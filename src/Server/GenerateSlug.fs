module GenerateSlug


open Akkling
open Model
open FCQRS.Model.Data

type SlugGeneration = 
        | SlugGenerated of Slug
        | SlugGenerationFailed of ShortString

let behavior (m: Actor<_>) =
    let rec loop () =
        actor {
            let! (mail: obj) = m.Receive()
            match mail with
            | :? Url as url ->
                let slug:Slug = System.Guid.NewGuid().ToString() |> ValueLens.CreateAsResult |> Result.value
                m.Sender().Tell(SlugGenerated slug, Akka.Actor.ActorRefs.NoSender)
                return! Stop
            | _ ->
                return! loop ()
        }

    loop ()