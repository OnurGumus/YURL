

module Query

open Microsoft.Extensions.Logging
open FCQRS.Common
open FCQRS.Model.Data
// All persisted events will come with monotonically increasing offset value.
let handleEventWrapper env (offsetValue: int64) (event:obj)=
    let loggerFactory = (env:>ILoggerFactoryWrapper).LoggerFactory
    let log = loggerFactory.CreateLogger "Event"
    log.LogInformation("Event: {0}", event.ToString())

    let dataEvent =
        match event with
        | :? FCQRS.Common.Event<UrlHash.Event> as  event ->
            printfn "!!Event: %A" event

            [event:> IMessageWithCID]
        |  _ -> []

    dataEvent