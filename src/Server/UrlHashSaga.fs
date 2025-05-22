module UrlHashSaga

open FCQRS
open Common
open Common.SagaStarter
open Akkling
open Model
open GenerateSlug
open FCQRS.Model.Data


type State =
    | NotStarted
    | Started of SagaStartingEvent<Event<UrlHash.Event>>
    | GeneratingSlug of Url
    | SuffixingSlug of Slug
    | ConfirmingSlug of Slug
    | RejectingSlug of ShortString
    | Completed

type SagaData = NA

let initialState = {
    State = NotStarted
    Data = (NA: SagaData)
}

let apply (sagaState: SagaState<SagaData, State>) = sagaState

let handleEvent (event: obj) (state: SagaState<SagaData, State>) = //: EventAction<State>  =
    match event, state with
    | :? SlugGeneration as SlugGenerated slug, _ -> SuffixingSlug slug |> StateChangedEvent
    | :? SlugGeneration as SlugGenerationFailed reason, _ -> RejectingSlug reason |> StateChangedEvent
    | :? (Common.Event<UrlHash.Event>) as { EventDetails = userEvent }, state ->
        match userEvent, state with
        | UrlHash.UrlProcessingStarted url, _ -> GeneratingSlug url |> StateChangedEvent
        | UrlHash.ProcessCompleted _, _ -> Completed |> StateChangedEvent
        | UrlHash.AlreadyProcessing, _ ->  Completed|> StateChangedEvent
        | _ -> UnhandledEvent

    | :? (Common.Event<SuffixSlug.Event>) as { EventDetails = userEvent }, state ->
        match userEvent, state with
        | SuffixSlug.SuffixGenerated(slug, _), _ -> ConfirmingSlug slug |> StateChangedEvent
        | _ -> UnhandledEvent

    | _ -> UnhandledEvent

let applySideEffects
    (actorRef: unit -> IActorRef<obj>)
    env
    userFactory
    suffixSlugFactory
    (sagaState: SagaState<SagaData, State>)
    (startingEvent: option<SagaStartingEvent<_>>)
    recovering
    =
    let originator =
        FactoryAndName {
            Factory = userFactory
            Name = Originator
        }

    match sagaState.State with
    | NotStarted -> NoEffect, Some(Started startingEvent.Value), []

    | Started _ ->
        if recovering then
            let startingEvent = startingEvent.Value.Event

            NoEffect,
            None,
            [
                {
                    TargetActor = originator
                    Command = ContinueOrAbort startingEvent
                    DelayInMs = None
                }
            ]
        else
            ResumeFirstEvent, None, []

    | GeneratingSlug url ->
        NoEffect,
        None,
        [
            {
                TargetActor = ActorRef(actorRef ())
                Command = url
                DelayInMs = None
            }
        ]

    | SuffixingSlug slug ->
        let suffixSlug =
            FactoryAndName {
                Factory = suffixSlugFactory
                Name = TargetName.Name(slug.ToString())
            }

        NoEffect,
        None,
        [
            {
                TargetActor = suffixSlug
                Command = SuffixSlug.GenerateSuffix slug
                DelayInMs = None
            }
        ]

    | ConfirmingSlug slug ->
        NoEffect,
        None,
        [
            {
                TargetActor = originator
                Command = UrlHash.Confirm slug
                DelayInMs = None
            }
        ]
    | RejectingSlug reason ->
        NoEffect,
        None,
        [
            {
                TargetActor = originator
                Command = UrlHash.Reject reason
                DelayInMs = None
            }
        ]

    | Completed -> StopActor, None, []

let init (env: _) (actorApi: IActor) =
    let userFactory = UrlHash.factory env actorApi
    let suffixSlugFactory = SuffixSlug.factory env actorApi

    let slugGenerator =
        fun () -> spawnAnonymous actorApi.System (props (GenerateSlug.behavior env)) |> retype

    actorApi.InitializeSaga
        env
        initialState
        handleEvent
        (applySideEffects slugGenerator env userFactory suffixSlugFactory)
        apply
        "UrlHashSaga"

let factory (env: _) actorApi entityId =
    (init env actorApi).RefFor DEFAULT_SHARD entityId
