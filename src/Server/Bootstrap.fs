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

    let mutable sub:ISubscribe<_>= Unchecked.defaultof<_>


    member this.GenerateSlug cid (url:Model.Url) =
        let urlString = url |> ValueLens.Value
        let hash = urlString |> generateHash
        let actorId: ActorId = Guid.NewGuid().ToString() |> ValueLens.CreateAsResult |> Result.value

        let command = url |> UrlHash.ProcessUrl

        let condition (e: UrlHash.Event) =
            e.IsAlreadyProcessed || e.IsAlreadyProcessing || e.IsProcessCompleted

        let subscribe = this.UrlHashSubs cid actorId command condition

        async {
            match! subscribe with
            | { EventDetails = UrlHash.ProcessCompleted ;  Version = v  }-> return Ok v
            | { EventDetails = UrlHash.AlreadyProcessing  }-> return Error [ sprintf "Url %s is already processing" <| urlString ]
            | { EventDetails = UrlHash.AlreadyProcessed  }-> return Error [ sprintf "Url %s is already processed" <| urlString ]

            | _ -> return Error [ sprintf "Unexpected event for registration %s" <| actorId.ToString() ]
        }


    member _.Sub = sub

    member _.UrlHashSubs cid =
        actorApi.CreateCommandSubscription urlHashShard cid


    override _.ExecuteAsync(_stoppingToken: CancellationToken) =
        task {

            let sagaCheck (o: obj) = []
            
            actorApi.InitializeSagaStarter sagaCheck

            UrlHash.init env actorApi |> ignore
            
            sub <- FCQRS.Query.init actorApi 0 (Query.handleEventWrapper env)

        }
