module OEmbedDiscovery

open System
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions

// Regex to find oEmbed discovery links in HTML
let private oembedLinkRegex =
    Regex(
        """<link[^>]+rel\s*=\s*["']alternate["'][^>]+type\s*=\s*["']application/json\+oembed["'][^>]+href\s*=\s*["']([^"']+)["']""",
        RegexOptions.IgnoreCase ||| RegexOptions.Compiled
    )

let private oembedLinkRegexXml =
    Regex(
        """<link[^>]+rel\s*=\s*["']alternate["'][^>]+type\s*=\s*["']text/xml\+oembed["'][^>]+href\s*=\s*["']([^"']+)["']""",
        RegexOptions.IgnoreCase ||| RegexOptions.Compiled
    )

type OEmbedInfo = {
    EndpointUrl: string
    Title: string option
}

let discoverOEmbed (url: string) =
    task {
        try
            use client = new HttpClient()
            client.Timeout <- TimeSpan.FromSeconds 10.0

            // Add headers to appear more like a browser
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; URL Shortener/1.0)")

            let! html = client.GetStringAsync(url)

            // Try to find JSON oEmbed link first (preferred)
            let jsonMatch = oembedLinkRegex.Match html

            let oembedUrl =
                if jsonMatch.Success then
                    Some jsonMatch.Groups.[1].Value
                else
                    // Fall back to XML if no JSON found
                    let xmlMatch = oembedLinkRegexXml.Match html

                    if xmlMatch.Success then
                        Some xmlMatch.Groups.[1].Value
                    else
                        None

            match oembedUrl with
            | Some endpoint ->
                // Make the endpoint URL absolute if it's relative
                let absoluteEndpoint =
                    if endpoint.StartsWith "http://" || endpoint.StartsWith "https://" then
                        endpoint
                    else if endpoint.StartsWith "//" then
                        let uri = Uri url
                        uri.Scheme + ":" + endpoint
                    else if endpoint.StartsWith "/" then
                        let uri = Uri url
                        uri.Scheme + "://" + uri.Host + endpoint
                    else
                        // Relative path
                        let uri = Uri(url)
                        let basePath = uri.GetLeftPart(UriPartial.Path)
                        let lastSlash = basePath.LastIndexOf('/')

                        if lastSlash >= 0 then
                            basePath.Substring(0, lastSlash + 1) + endpoint
                        else
                            basePath + "/" + endpoint

                return Some absoluteEndpoint
            | None -> return None

        with _ ->
            return None
    }

let getOEmbedData (oembedUrl: string) =
    task {
        try
            use client = new HttpClient()
            client.Timeout <- TimeSpan.FromSeconds 10.0

            let! response = client.GetAsync oembedUrl
            response.EnsureSuccessStatusCode() |> ignore

            let! jsonContent = response.Content.ReadAsStringAsync()
            let jsonDoc = JsonDocument.Parse jsonContent

            let title =
                match jsonDoc.RootElement.TryGetProperty "title" with
                | true, titleElement ->
                    let titleStr = titleElement.GetString()

                    if String.IsNullOrWhiteSpace titleStr then
                        None
                    else
                        Some(titleStr |> Unchecked.nonNull<_>)
                | false, _ -> None

            return
                Some {
                    EndpointUrl = oembedUrl
                    Title = title
                }
        with _ ->
            return None
    }
