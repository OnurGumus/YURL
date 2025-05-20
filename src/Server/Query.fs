module Query

open Microsoft.Extensions.Logging
open FCQRS.Common
open FCQRS.Model.Data
open FSharp.Data.Sql.Common
open Akka.Persistence.Query
open SqlProvider
open System
open Microsoft.Extensions.Configuration
open System.Threading.Tasks

type Url = { Slug: string; OriginalUrl: string }

let queryApi env =
    let config = (env :> IConfigurationWrapper).Configuration
    let connString = config.GetSection("config:connection-string").Value

    let query
        (
            ty: System.Type,
            filter,
            orderby,
            orderbydesc,
            thenby,
            thenbydesc,
            take: int option,
            skip,
            (cacheKey: string option)
        ) : Async<obj seq> =
        let ctx = Sql.GetDataContext connString
        //gets an augment to apply the fltering and paging. Internal FCQRS stuff here
        let augment db =
            FCQRS.SQLProvider.Query.augmentQuery filter orderby orderbydesc thenby thenbydesc take skip db

        let res: Task<obj seq> =
            task {
                if ty = typeof<Url> then
                    let credit =
                        query {
                            for c in ctx.Main.Urls do
                                select c
                        }

                    return
                        augment <@ credit @>
                        |> Seq.map (fun x ->
                            {
                                Slug = x.Slug
                                OriginalUrl = x.OriginalUrl
                            }
                            : Url)

                else
                    return failwithf "Type %A not supported" ty
            }

        res |> Async.AwaitTask

    query


// All persisted events will come with monotonically increasing offset value.
let handleEventWrapper env (ctx: Sql.dataContext) (offsetValue: int64) (event: obj) =
    let loggerFactory = (env :> ILoggerFactoryWrapper).LoggerFactory
    let log = loggerFactory.CreateLogger "Event"
    log.LogInformation("Event: {0}", event.ToString())

    let dataEvent =
        match event with
        | :? FCQRS.Common.Event<UrlHash.Event> as event ->
            match event.EventDetails with
            | UrlHash.ProcessCompleted(ResultValue url, Value(ResultValue slug)) ->
                let row =
                    ctx.Main.Urls.``Create(CreatedAt, UpdatedAt, Version)`` (
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        event.Version |> ValueLens.Value
                    )

                row.Slug <- slug
                row.OriginalUrl <- url

                [ event :> IMessageWithCID ]
            | _ -> []
        | _ -> []

    let offset = ctx.Main.Offsets.Individuals.Shorten
    offset.OffsetCount <- offsetValue
    ctx.SubmitUpdates()
    dataEvent

let init env (actorApi: IActor) =
    let config = (env :> IConfigurationWrapper).Configuration
    let ctx = Sql.GetDataContext config.["config:connection-string"]

    use conn = ctx.CreateConnection()
    conn.Open()
    let cmd = conn.CreateCommand()
    cmd.CommandText <- "PRAGMA journal_mode=WAL;"
    cmd.ExecuteNonQuery() |> ignore

    let offsetCount = ctx.Main.Offsets.Individuals.Shorten.OffsetCount
    FCQRS.Query.init actorApi offsetCount (handleEventWrapper env ctx)
