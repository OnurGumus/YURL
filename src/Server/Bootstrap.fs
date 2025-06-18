module Bootstrap
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open System.Threading
open FCQRS.Model.Data
open FCQRS.Query
open FCQRS.Common
open System
open System.Text
open System.Security.Cryptography

let generateHash (input: string) : string =
    use sha256 = SHA256.Create()
    let bytes = Encoding.UTF8.GetBytes input
    let hash = sha256.ComputeHash bytes
    BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()

type CQRSService(env: Environments.AppEnv) =
    inherit BackgroundService()
    let actorApi = FCQRS.Actor.api env
    let urlHashShard = UrlHash.factory env actorApi
    let suffixSlugShard = SuffixSlug.factory env actorApi
    let urlHashSaga = UrlHashSaga.factory env actorApi

    let mutable sub:ISubscribe<_>= Unchecked.defaultof<_>
    let mutable queryApi = Unchecked.defaultof<_>


    member this.GenerateSlug cid (url:Model.Url) =
        let urlString = url |> ValueLens.Value
        let hash = "UrlHash_" + urlString |> generateHash
        let actorId: ActorId = hash |> ValueLens.CreateAsResult |> Result.value

        let command = url |> UrlHash.ProcessUrl

        let condition (e: UrlHash.Event) =
            e.IsAlreadyProcessed || e.IsAlreadyProcessing || e.IsProcessCompleted

        let subscribe = this.UrlHashSubs cid actorId command condition None

        async {
            match! subscribe with
            | { EventDetails = UrlHash.ProcessCompleted _ ;  Version = v  }-> return Ok v
            | { EventDetails = UrlHash.UrlProcessingFailed (ResultValue reason)} -> return Error [ sprintf "Url %s processing failed: %s" urlString reason ]
            | { EventDetails = UrlHash.AlreadyProcessing  }-> return Error [ sprintf "Url %s is already processing" <| urlString ]
            | { EventDetails = UrlHash.AlreadyProcessed  }-> return Error [ sprintf "Url %s is already processed" <| urlString ]

            | _ -> return Error [ sprintf "Unexpected event for registration %s" <| actorId.ToString() ]
        }


    member _.Sub = sub

    member _.Query<'t>(?filter, ?orderby, ?orderbydesc, ?thenby, ?thenbydesc, ?take, ?skip, ?cacheKey) = async{
        let! res = queryApi (typeof<'t>, filter, orderby, orderbydesc, thenby, thenbydesc, take, skip, cacheKey)
        return res |> Seq.cast<'t> |> List.ofSeq
    }

    member _.UrlHashSubs cid = actorApi.CreateCommandSubscription urlHashShard cid  
       


    override _.ExecuteAsync(_stoppingToken: CancellationToken) =
        task {
            Migrations.init env

            let sagaCheck (o: obj) =
                match o with
                | :? (FCQRS.Common.Event<UrlHash.Event>) as e ->
                    match e.EventDetails with
                    | UrlHash.UrlProcessingStarted _ -> [ urlHashSaga, id |> Some |> PrefixConversion, o ]
                    | _ -> []
                | _ -> []

            
            actorApi.InitializeSagaStarter sagaCheck

            UrlHash.init env actorApi |> ignore
            SuffixSlug.init env actorApi |> ignore
            UrlHashSaga.init env actorApi |> ignore
            
            sub <- Query.init env actorApi
            queryApi <- Query.queryApi env
        }
