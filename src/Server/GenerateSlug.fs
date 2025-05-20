module GenerateSlug

open Akkling
open Model
open FCQRS.Model.Data
open System.Security.Cryptography
open System.Text

type SlugGeneration = 
        | SlugGenerated of Slug
        | SlugGenerationFailed of ShortString

let generateHash (input: string) =
    use sha256 = SHA256.Create()
    let hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input))
    let hashString = System.Convert.ToBase64String(hashBytes)
    // Take first 8 characters and replace URL-unsafe characters
    hashString.Substring(0, 8)
        .Replace("+", "-")
        .Replace("/", "_")
        .Replace("=", "")

let behavior (m: Actor<_>) =
    let rec loop () =
        actor {
            let! (mail: obj) = m.Receive()
            match mail with
            | :? Url as (ResultValue url) ->
                let slug:Slug = generateHash url |> ValueLens.CreateAsResult |> Result.value
                m.Sender().Tell(SlugGenerated slug, Akka.Actor.ActorRefs.NoSender)
                return! Stop
            | _ ->
                return! loop ()
        }

    loop ()