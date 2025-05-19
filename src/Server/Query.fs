module Query

open Microsoft.Extensions.Logging
open FCQRS.Common
open FCQRS.Model.Data
open FSharp.Data.Sql.Common
open Akka.Persistence.Query
open SqlProvider
open System

// All persisted events will come with monotonically increasing offset value.
let handleEventWrapper env (ctx: Sql.dataContext) (offsetValue: int64) (event:obj)=
    let loggerFactory = (env:>ILoggerFactoryWrapper).LoggerFactory
    let log = loggerFactory.CreateLogger "Event"
    log.LogInformation("Event: {0}", event.ToString())

    let dataEvent =
        match event with
        | :? FCQRS.Common.Event<UrlHash.Event> as  event ->
            match event.EventDetails with
            | UrlHash.ProcessCompleted (ResultValue url,Value (ResultValue slug)) ->
                let row = ctx.Main.Urls.``Create(CreatedAt, UpdatedAt, Version)``(
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    event.Version |> ValueLens.Value
                ) 
                row.Slug <- slug 
                row.OriginalUrl <- url 
     
                [event:> IMessageWithCID]
            | _ ->[]
        |  _ -> []
 
    let offset = ctx.Main.Offsets.Individuals.Shorten
    offset.OffsetCount <- offsetValue
    ctx.SubmitUpdates()
    dataEvent

let init env  (actorApi: IActor) =
        let config = (env:>IConfigurationWrapper).Configuration
        let ctx = Sql.GetDataContext config.["config:connection-string"]
    
        use conn = ctx.CreateConnection()
        conn.Open()
        let cmd = conn.CreateCommand()
        cmd.CommandText <- "PRAGMA journal_mode=WAL;"
        cmd.ExecuteNonQuery() |> ignore
    
        let offsetCount =  ctx.Main.Offsets.Individuals.Shorten.OffsetCount
        FCQRS.Query.init actorApi offsetCount (handleEventWrapper env ctx) 