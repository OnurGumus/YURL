module GenerateSlug


open Akkling
open Model
open FCQRS.Model.Data

type SlugGenerated = SlugGenerated of Slug

let behavior (m: Actor<_>) =
    let rec loop () =
        actor {
            let! (mail: obj) = m.Receive()
            match mail with
            | :? Url as url ->
                printfn "\n!!!!Generating Slug mail to %A !!" url
                let slug:Slug = System.Guid.NewGuid().ToString() |> ValueLens.CreateAsResult |> Result.value
                m.Sender().Tell(SlugGenerated slug, Akka.Actor.ActorRefs.NoSender)
                return! Stop
            | _ ->
                return! loop ()
        }

    loop ()