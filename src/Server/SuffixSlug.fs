module SuffixSlug

open FCQRS.Common
open Model
open FCQRS.Model.Data

type Suffix = NoSuffix | Suffix of int

type Event =
    | SuffixGenerated of Slug * Suffix
    | SlugMismatch of Slug * Slug

type Command =
    | GenerateSuffix of Slug


type State = { LastSuffix: Suffix; RootSlug: Slug option }

let applyEvent event state =
    match event.EventDetails with
    | SuffixGenerated (slug, suffix) -> { state with LastSuffix = suffix; RootSlug = Some slug }
    | _ -> state

let handleCommand (cmd: Command<_>) state =
    match cmd.CommandDetails, (state:State) with
    
    | GenerateSuffix slug, { RootSlug = None} ->
        SuffixGenerated (slug, NoSuffix) |> PersistEvent

    | GenerateSuffix (Value (ResultValue baseUrl) as newSlug), { RootSlug = Some slug} ->
        if slug <> newSlug then
            SlugMismatch (slug, newSlug) |> DeferEvent
        else
            let newBasUrl, suffix = 
                match state.LastSuffix with
                | NoSuffix -> baseUrl + "1", Suffix 1
                | Suffix s ->baseUrl + (s + 1).ToString(), Suffix (s + 1)
            let newSlug:Slug = newBasUrl |> ValueLens.CreateAsResult |> Result.value
            SuffixGenerated (newSlug, suffix) |> PersistEvent


let init (env: _) (actorApi: IActor) =
    let initialState = {LastSuffix = NoSuffix; RootSlug = None}
    actorApi.InitializeActor env initialState "SuffixSlug" handleCommand applyEvent

let factory (env: #_) actorApi entityId =
    (init env actorApi).RefFor DEFAULT_SHARD entityId