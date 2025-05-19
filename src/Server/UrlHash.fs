module UrlHash

open FCQRS.Common
open FCQRS.Model.Data
open Model

type Event =
    | ProcessCompleted of Url *Slug
    | UrlProcessingFailed of ShortString
    | UrlProcessingStarted of Url
    | AlreadyProcessing
    | AlreadyProcessed

type Command =
    | ProcessUrl of Url
    | Confirm of Slug
    | Reject of ShortString

type ProcessState =
    | NotStarted
    | Processing
    | Completed 

type State = { ProcessState: ProcessState; OriginalUrl: Url option; Slug: Slug option }

let applyEvent event state =
    match event.EventDetails with
    | ProcessCompleted (url, slug) -> { state with ProcessState = Completed; Slug = Some slug; OriginalUrl = Some url }
    | UrlProcessingStarted url -> { state with ProcessState = Processing ; OriginalUrl = Some url ; }
    | _ -> state

let handleCommand (cmd: Command<_>) state =
    match cmd.CommandDetails, state.ProcessState with
    | ProcessUrl url, NotStarted -> UrlProcessingStarted url |> PersistEvent
    | Confirm slug, Processing -> ProcessCompleted (state.OriginalUrl.Value, slug) |> PersistEvent
    | ProcessUrl _,  Processing -> AlreadyProcessing |> DeferEvent
    | ProcessUrl _, Completed -> AlreadyProcessed |> DeferEvent
    | Confirm _, _ -> AlreadyProcessed |> DeferEvent
    | Reject reason, Processing -> UrlProcessingFailed reason |> PersistEvent
    | Reject _, _ -> AlreadyProcessed |> DeferEvent

let init (env: _) (actorApi: IActor) =
    let initialState = { ProcessState = NotStarted; OriginalUrl = None; Slug = None }
    actorApi.InitializeActor env initialState "UrlHash" handleCommand applyEvent

let factory (env: #_) actorApi entityId =
    (init env actorApi).RefFor DEFAULT_SHARD entityId