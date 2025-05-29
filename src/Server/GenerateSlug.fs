module GenerateSlug

#nowarn 57

open Akkling
open Model
open FCQRS.Model.Data
open System.Security.Cryptography
open System.Text
open System
open System.Net.Http
open Microsoft.SemanticKernel.Agents.OpenAI
open System.ClientModel
open Microsoft.SemanticKernel.ChatCompletion
open FSharp.Control
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open System.Diagnostics

let client = OpenAIAssistantAgent.CreateOpenAIClient()

type SlugGeneration =
    | SlugGenerated of Slug
    | SlugGenerationFailed of ShortString

let private isHtmlContentType (contentType: string | null) =
    match contentType with
    | null -> false
    | contentType ->
        contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase)

let private checkContentType (url: string) =
    task {
        try
            use client = new HttpClient()
            use request = new HttpRequestMessage(HttpMethod.Head, url)
            let! response = client.SendAsync request

            let contentType =
                match response.Content.Headers.ContentType with
                | null -> None
                | contentType -> Some contentType.MediaType

            return contentType |> Option.map isHtmlContentType |> Option.defaultValue false
        with _ ->
            return false
    }

let private getHtmlContent (url: string) =
    task {
        try
            use client = new HttpClient()
            client.Timeout <- TimeSpan.FromSeconds(10.0)

            let! response = client.GetAsync(url)
            response.EnsureSuccessStatusCode() |> ignore

            let! fullHtmlContent = response.Content.ReadAsStringAsync()

            let initialProcessingLength = 4096
            let additionalProcessingLength = 3000
            let maxOutputLength = 2000

            let titleRegex = Regex("<title>([^<]+)</title>", RegexOptions.IgnoreCase)

            let metaRegex =
                Regex(
                    """<meta(?=[^>]*name\s*=\s*['"](description|keywords)['"])(?=[^>]*content\s*=\s*['"]([^'"]*)['"])[^>]*>""",
                    RegexOptions.IgnoreCase ||| RegexOptions.Singleline
                )

            let extractTags (htmlChunk: string) =
                let titleMatch = titleRegex.Match htmlChunk

                let title =
                    if titleMatch.Success then
                        titleMatch.Groups.[1].Value.Trim()
                    else
                        ""

                let metaMatches = metaRegex.Matches htmlChunk

                let metaContent =
                    metaMatches
                    |> Seq.cast<Match>
                    |> Seq.map (fun m -> m.Groups.[2].Value.Trim())
                    |> Seq.filter (fun s -> not (String.IsNullOrWhiteSpace s))
                    |> String.concat " "

                (title + " " + metaContent).Trim()

            let getLimitedSubstring (text: string) (maxLength: int) =
                if String.IsNullOrWhiteSpace text then
                    ""
                else
                    let effectiveLength = min maxLength text.Length
                    text.Substring(0, effectiveLength)

            let initialHtmlChunk = getLimitedSubstring fullHtmlContent initialProcessingLength
            let mutable extractedContent = extractTags initialHtmlChunk

            if
                String.IsNullOrWhiteSpace(extractedContent)
                && fullHtmlContent.Length > initialProcessingLength
            then
                let extendedProcessingLength = initialProcessingLength + additionalProcessingLength
                let extendedHtmlChunk = getLimitedSubstring fullHtmlContent extendedProcessingLength
                extractedContent <- extractTags extendedHtmlChunk

            if String.IsNullOrWhiteSpace(extractedContent) then
                return getLimitedSubstring initialHtmlChunk maxOutputLength
            else
                return getLimitedSubstring extractedContent maxOutputLength
        with _ ->
            return url
    }

let private prepareForOpenAI (url: string) =
    task {
        let! isHtml = checkContentType url
        let urlPrefix = url.Substring(0, min 50 url.Length) // Get first 50 chars of URL

        if isHtml then
            let! htmlBasedContent = getHtmlContent url
            // Prepend URL prefix to the HTML-based content
            let combinedContent = urlPrefix + " " + htmlBasedContent
            // Ensure the combined content does not exceed a reasonable length for OpenAI
            let maxOpenAiInputLength = 1000 // Reduced to 1000

            if combinedContent.Length > maxOpenAiInputLength then
                return combinedContent.Substring(0, maxOpenAiInputLength)
            else
                return combinedContent
        else
            // If not HTML, return the original URL (OpenAI will likely use the full URL)
            // Or, if we want to be consistent, just the prefix or the full URL capped.
            // For now, returning the full URL as before for non-HTML.
            return url
    }

let generateHash (input: string) =
    use sha256 = SHA256.Create()
    let hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input))
    let hashString = System.Convert.ToBase64String(hashBytes)
    // Take first 8 characters and replace URL-unsafe characters
    hashString.Substring(0, 8).Replace("+", "-").Replace("/", "_").Replace("=", "")

let generateSlug url openAiKey assistantId (log: ILogger) =
    task {
        let sw = Stopwatch.StartNew()

        try
            let! contentForOpenAI = url |> prepareForOpenAI

            let client =
                openAiKey
                |> nonNull
                |> ApiKeyCredential
                |> OpenAIAssistantAgent.CreateOpenAIClient
                |> _.GetAssistantClient()

            let assistant = client.GetAssistant assistantId
            let agent = OpenAIAssistantAgent(assistant, client)

            log.LogInformation("Content for OpenAI: {content}, time: {time}", contentForOpenAI, sw.Elapsed)

            let! finalSlug =
                (agent.InvokeAsync(Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, contentForOpenAI))
                 |> TaskSeq.map _.Message.Content
                 |> TaskSeq.head)
                |> Async.AwaitTask

            log.LogInformation("Final slug: {slug}, time: {time}", finalSlug, sw.Elapsed)
            return finalSlug |> ValueLens.CreateAsResult |> Result.value
        with ex ->
            log.LogError(ex, "Error generating slug for URL: {url}", url)
            return generateHash url |> ValueLens.CreateAsResult |> Result.value
    }

let behavior env (m: Actor<_>) =
    let config = (env :> FCQRS.Common.IConfigurationWrapper).Configuration

    let log =
        (env :> FCQRS.Common.ILoggerFactoryWrapper).LoggerFactory.CreateLogger "SlugGeneration"
    // Asisstant Prompt:
    //Analyze the input text. Identify two primary keywords or the most salient short words.
    //Combine these into a lowercase URL slug, `keyword1_keyword2`.
    // The slug must be under 15 characters total.
    //Return only the slug.
    let assistantId =
        match config.["config:assistant-id"] with
        | null -> Environment.GetEnvironmentVariable "ASSISTANT_ID"
        | id -> id

    let openAiKey =
        match config.["config:openai-api-key"] with
        | null -> Environment.GetEnvironmentVariable "OPENAI_API_KEY"
        | key -> key

    let rec loop () =
        actor {
            let! (mail: obj) = m.Receive()

            match mail with
            | :? Url as ResultValue url ->
                let! slug =
                    task {
                        if String.IsNullOrEmpty openAiKey || String.IsNullOrEmpty assistantId then
                            return generateHash url |> ValueLens.CreateAsResult |> Result.value
                        else
                            try
                                return! generateSlug url openAiKey assistantId log
                            with ex ->
                                log.LogError(ex, "Error generating slug for URL: {url}. Fallback to hash", url)
                                return generateHash url |> ValueLens.CreateAsResult |> Result.value
                    }

                m.Sender().Tell(SlugGenerated slug, Akka.Actor.ActorRefs.NoSender)
                return! Stop

            | _ -> return! loop ()
        }

    loop ()
