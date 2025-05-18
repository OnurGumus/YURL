module PrefixSlug

open FCQRS.Common
open Model

type Event =
    | PrefixGenerated of Slug

type Command =
    | GenratePrefix of Url

type r =
    | NotStarted
    | Processing
    | Completed 

type State = { LastPrefix: ProcessState }

let applyEvent event state =
    match event.EventDetails with
    | ProcessCompleted -> { state with ProcessState = Completed }
    | UrlProcessingStarted url -> { state with ProcessState = Processing }
    | _ -> state

let handleCommand (cmd: Command<_>) state =
    match cmd.CommandDetails, state.ProcessState with
    | ProcessUrl url, NotStarted -> UrlProcessingStarted url |> PersistEvent
    | CompleteProcess, Processing -> ProcessCompleted |> PersistEvent
    | ProcessUrl _,  Processing -> AlreadyProcessing |> DeferEvent
    | ProcessUrl _, Completed -> AlreadyProcessed |> DeferEvent
    | CompleteProcess, _ -> AlreadyProcessed |> DeferEvent

let init (env: _) (actorApi: IActor) =
    let initialState = { ProcessState = NotStarted }
    actorApi.InitializeActor env initialState "UrlHash" handleCommand applyEvent

let factory (env: #_) actorApi entityId =
    (init env actorApi).RefFor DEFAULT_SHARD entityId