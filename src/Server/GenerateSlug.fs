module GenerateSlug
#nowarn 57
open Akkling
open Model
open FCQRS.Model.Data
open System.Security.Cryptography
open System.Text
open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.SemanticKernel.Agents.OpenAI
open System.ClientModel
open OpenAI.Chat
open Microsoft.SemanticKernel.ChatCompletion
open System.Collections.Generic
open FSharp.Control
// open Microsoft.SemanticKernel.Agents.OpenAI
let client = OpenAIAssistantAgent.CreateOpenAIClient()

type SlugGeneration = 
        | SlugGenerated of Slug
        | SlugGenerationFailed of ShortString

let private isHtmlContentType (contentType: string | null) =
    match contentType with  
    | null -> false
    | contentType -> 
        contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
        contentType.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase)

let private checkContentType (url: string) =
    async {
        try
            use client = new HttpClient()
            use request = new HttpRequestMessage(HttpMethod.Head, url)
            let! response = client.SendAsync request |> Async.AwaitTask
            let contentType = 
                match response.Content.Headers.ContentType with
                | null -> None
                | contentType -> Some contentType.MediaType 

            return contentType |> Option.map isHtmlContentType |> Option.defaultValue false
        with
        | _ -> return false
    }

let private getHtmlContent (url: string) =
    async {
        try
            use client = new HttpClient()
            let! response = url |> client.GetAsync |> Async.AwaitTask
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return content.Substring(0, min 1000 content.Length)
        with
        | _ -> return url
    }

let private prepareForOpenAI (url: string) =
    async {
        let! isHtml = checkContentType url
        if isHtml then
            // If it's HTML, download first 200 bytes
            return! getHtmlContent url
        else
            return url
    }

let generateHash (input: string) =
    use sha256 = SHA256.Create()
    let hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input))
    let hashString = System.Convert.ToBase64String(hashBytes)
    // Take first 8 characters and replace URL-unsafe characters
    hashString.Substring(0, 8)
        .Replace("+", "-")
        .Replace("/", "_")
        .Replace("=", "")

let behavior env (m: Actor<_>) =
    let config = (env :> FCQRS.Common.IConfigurationWrapper).Configuration
    let assistantId = 
        match config.["config:assistant-id"] with
        | null -> Environment.GetEnvironmentVariable("ASSISTANT_ID")
        | id -> id
    let openAiKey = 
        match config.["config:openai-api-key"] with
        | null -> Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        | key -> key

    let rec loop () =
        actor {
            let! (mail: obj) = m.Receive()
            match mail with
            | :? Url as ResultValue url ->
                if String.IsNullOrEmpty openAiKey then
                    // Original hash generation if OpenAI API key is not available
                    let slug:Slug = generateHash url |> ValueLens.CreateAsResult |> Result.value
                    m.Sender().Tell(SlugGenerated slug, Akka.Actor.ActorRefs.NoSender)
                else
                    // OpenAI based approach
                    let contentForOpenAI = (prepareForOpenAI url) |> Async.RunSynchronously
                    let client = 
                        openAiKey
                        |> nonNull
                        |>ApiKeyCredential
                        |>OpenAIAssistantAgent.CreateOpenAIClient
                        |> _.GetAssistantClient()
                    let assistant = 
                        client.GetAssistant assistantId
                    let agent = OpenAIAssistantAgent(assistant, client)
                    let z =
                        taskSeq{
                            for response in agent.InvokeAsync(Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User,contentForOpenAI))  do
                                 yield response.Message.Content
                        }
                    let finalSlug = (z  |> TaskSeq.head).Result
                    
                    let slug:Slug =finalSlug  |> ValueLens.CreateAsResult |> Result.value
                    m.Sender().Tell(SlugGenerated slug, Akka.Actor.ActorRefs.NoSender)
                
                return! Stop
            | _ ->
                return! loop ()
        }

    loop ()