module UrlHash

open FCQRS.Common
open Model

type Event =
    | ProcessCompleted
    | UrlProcessingStarted of Url
    | AlreadyProcessing
    | AlreadyProcessed

type Command =
    | ProcessUrl of Url
    | CompleteProcess

type ProcessState =
    | NotStarted
    | Processing
    | Completed 

type State = { ProcessState: ProcessState }

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