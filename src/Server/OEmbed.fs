module OEmbed

open System
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions
open System.Web

type OEmbedProvider = {
    Name: string
    UrlPattern: Regex
    OEmbedEndpoint: string -> string
    SlugPrefix: string
    SlugProcessor: (string -> string) option
}

// Helper function to trim 3-word slugs to 2 words
let private trimThreeWords (slug: string) =
    let parts = slug.Split('_')

    if parts.Length = 3 then
        parts.[0] + "_" + parts.[1]
    else
        slug

// Define oEmbed providers
let providers = [
    {
        Name = "YouTube"
        UrlPattern = Regex(@"(?:youtube\.com/watch\?v=|youtu\.be/|youtube\.com/embed/|youtube\.com/shorts/)", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> 
                // Convert YouTube Shorts URL to regular video URL for oEmbed
                let convertedUrl = 
                    if url.Contains("/shorts/") then
                        let videoIdMatch = Regex.Match(url, @"youtube\.com/shorts/([a-zA-Z0-9_-]+)")
                        if videoIdMatch.Success then
                            let videoId = videoIdMatch.Groups.[1].Value
                            $"https://www.youtube.com/watch?v={videoId}"
                        else
                            url
                    else
                        url
                $"https://www.youtube.com/oembed?url={System.Web.HttpUtility.UrlEncode(convertedUrl)}&format=json"
        SlugPrefix = "ytb_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Vimeo"
        UrlPattern = Regex(@"vimeo\.com/\d+", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://vimeo.com/api/oembed.json?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "vim_"
        SlugProcessor = None
    }
    {
        Name = "TikTok"
        UrlPattern = Regex(@"tiktok\.com/.+/video/\d+", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.tiktok.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "ttk_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Spotify"
        UrlPattern = Regex(@"open\.spotify\.com/(track|album|playlist|episode)/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://open.spotify.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "spt_"
        SlugProcessor = None
    }
    {
        Name = "SoundCloud"
        UrlPattern = Regex(@"soundcloud\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://soundcloud.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}&format=json"
        SlugPrefix = "snd_"
        SlugProcessor = None
    }
    {
        Name = "Instagram"
        UrlPattern = Regex(@"instagram\.com/(?:p|reel|tv)/[\w-]+", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://api.instagram.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "ig_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Bluesky"
        UrlPattern = Regex(@"bsky\.app/profile/.+/post/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url ->
                // Bluesky uses AT Protocol URLs for oEmbed
                let atUrl =
                    url.Replace("https://bsky.app/profile/", "at://").Replace("/post/", "/app.bsky.feed.post/")

                $"https://embed.bsky.app/oembed?url={System.Web.HttpUtility.UrlEncode(atUrl)}"
        SlugPrefix = "bsk_"
        SlugProcessor = None
    }
    {
        Name = "Reddit"
        UrlPattern = Regex(@"reddit\.com/r/\w+/comments/\w+", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.reddit.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "rdt_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "TED"
        UrlPattern = Regex(@"ted\.com/talks/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://www.ted.com/services/v1/oembed.json?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "ted_"
        SlugProcessor = None
    }
    {
        Name = "Medium"
        UrlPattern = Regex(@"medium\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://medium.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "med_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Twitch"
        UrlPattern = Regex(@"twitch\.tv/(?:videos/\d+|\w+/clip/\w+)", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://api.twitch.tv/v5/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "twh_"
        SlugProcessor = None
    }
    {
        Name = "Flickr"
        UrlPattern = Regex(@"flickr\.com/photos/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://www.flickr.com/services/oembed?url={System.Web.HttpUtility.UrlEncode(url)}&format=json"
        SlugPrefix = "flk_"
        SlugProcessor = None
    }
    {
        Name = "SlideShare"
        UrlPattern = Regex(@"slideshare\.net/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url ->
                $"https://www.slideshare.net/api/oembed/2?url={System.Web.HttpUtility.UrlEncode(url)}&format=json"
        SlugPrefix = "sld_"
        SlugProcessor = None
    }
    {
        Name = "Mixcloud"
        UrlPattern = Regex(@"mixcloud\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://app.mixcloud.com/oembed/?url={System.Web.HttpUtility.UrlEncode(url)}&format=json"
        SlugPrefix = "mix_"
        SlugProcessor = None
    }
    {
        Name = "DailyMotion"
        UrlPattern = Regex(@"dailymotion\.com/video/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url ->
                $"https://www.dailymotion.com/services/oembed?url={System.Web.HttpUtility.UrlEncode(url)}&format=json"
        SlugPrefix = "dm_"
        SlugProcessor = None
    }
    {
        Name = "CodePen"
        UrlPattern = Regex(@"codepen\.io/\w+/pen/\w+", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://codepen.io/api/oembed?url={System.Web.HttpUtility.UrlEncode(url)}&format=json"
        SlugPrefix = "cpn_"
        SlugProcessor = None
    }
    {
        Name = "The Guardian"
        UrlPattern = Regex(@"theguardian\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://embed.theguardian.com/embed/oembed.json?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "gdn_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "New York Times"
        UrlPattern = Regex(@"nytimes\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://www.nytimes.com/svc/oembed/json/?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "nyt_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Washington Post"
        UrlPattern = Regex(@"washingtonpost\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://www.washingtonpost.com/wp-srv/oembed/json/?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "wpo_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "CNN"
        UrlPattern = Regex(@"cnn\.com/videos/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://api.cnn.io/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "cnn_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "BBC"
        UrlPattern = Regex(@"bbc\.(?:com|co\.uk)/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.bbc.co.uk/oembed/?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "bbc_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Financial Times"
        UrlPattern = Regex(@"ft\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.ft.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "ft_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Bloomberg"
        UrlPattern = Regex(@"bloomberg\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.bloomberg.com/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "blm_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Reuters"
        UrlPattern = Regex(@"reuters\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.reuters.com/tools/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "reu_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Wall Street Journal"
        UrlPattern = Regex(@"wsj\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.wsj.com/api-video/oembed/?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "wsj_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "NPR"
        UrlPattern = Regex(@"npr\.org/", RegexOptions.IgnoreCase)
        OEmbedEndpoint =
            fun url -> $"https://www.npr.org/oembed?url={System.Web.HttpUtility.UrlEncode(url)}&format=json"
        SlugPrefix = "npr_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "Associated Press"
        UrlPattern = Regex(@"apnews\.com/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://interactives.ap.org/oembed/?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "ap_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "The Independent"
        UrlPattern = Regex(@"independent\.co\.uk/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.independent.co.uk/oembed?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "ind_"
        SlugProcessor = Some trimThreeWords
    }
    {
        Name = "The Telegraph"
        UrlPattern = Regex(@"telegraph\.co\.uk/", RegexOptions.IgnoreCase)
        OEmbedEndpoint = fun url -> $"https://www.telegraph.co.uk/oembed/?url={System.Web.HttpUtility.UrlEncode(url)}"
        SlugPrefix = "tel_"
        SlugProcessor = Some trimThreeWords
    }
]

let findProvider (url: string) =
    providers |> List.tryFind (fun p -> p.UrlPattern.IsMatch(url))

let getTitle (url: string) =
    task {
        match findProvider url with
        | Some provider ->
            try
                use client = new HttpClient()
                client.Timeout <- TimeSpan.FromSeconds(10.0)

                let oembedUrl = provider.OEmbedEndpoint url

                let! response = client.GetAsync(oembedUrl)
                response.EnsureSuccessStatusCode() |> ignore

                let! jsonContent = response.Content.ReadAsStringAsync()
                let jsonDoc = JsonDocument.Parse(jsonContent)

                match jsonDoc.RootElement.TryGetProperty "title" with
                | true, titleElement ->
                    let title = titleElement.GetString()

                    return
                        if String.IsNullOrWhiteSpace title then
                            url
                        else
                            title |> Unchecked.nonNull
                | false, _ -> return url
            with _ ->
                return url
        | None -> return url
    }

let processSlug (url: string) (rawSlug: string) =
    match findProvider url with
    | Some provider ->
        let processedSlug =
            match provider.SlugProcessor with
            | Some processor -> processor (rawSlug.Trim())
            | None -> rawSlug.Trim()

        provider.SlugPrefix + processedSlug
    | None -> rawSlug

let isOEmbedSupported (url: string) = findProvider url |> Option.isSome

// Legacy functions for backward compatibility
let isYouTubeUrl (url: string) =
    providers
    |> List.find (fun p -> p.Name = "YouTube")
    |> fun p -> p.UrlPattern.IsMatch(url)
