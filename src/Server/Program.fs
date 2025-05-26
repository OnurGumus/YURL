module Program

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Giraffe
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open Fue.Data
open Fue.Compiler
open System.IO
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open System
open ThrottlingTroll
open Hocon.Extensions.Configuration
open FCQRS.Model.Data
open FsToolkit.ErrorHandling

#if DEBUG
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
#endif

[<CLIMutable>]
type UrlRequest = { Url: string }

let ensureUrlHasProtocol (url: string) =
    if url.StartsWith("http://") || url.StartsWith("https://") then
        url
    else
        "https://" + url

let cid (): CID =
    Guid.CreateVersion7().ToString() |> ValueLens.CreateAsResult |> Result.value

let builder = WebApplication.CreateBuilder()

let indexPageHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let contentRootPath = ctx.GetWebHostEnvironment().ContentRootPath
        let indexHtml = Path.Combine(contentRootPath, "templates", "index.html")

        task {
            let compiledHtml = init |> add "someValue" "myValue" |> fromFile indexHtml
            return! htmlString compiledHtml next ctx
        }

let slugHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let! url = ctx.BindModelAsync<UrlRequest>()
            let originalUrl = ensureUrlHasProtocol url.Url
            let cqrsService = ctx.GetService<Bootstrap.CQRSService>()
            
            // First check if URL already exists
            let! existingUrl = 
                cqrsService.Query<Query.Url>(filter = Predicate.Equal("OriginalUrl", originalUrl), take = 1) 
                |> Async.map (fun urls -> urls |> Seq.tryHead)
            
            match existingUrl with
            | Some existing ->
                // URL already exists, return existing slug
                return! json existing.Slug next ctx
            | None ->
                // URL doesn't exist, generate new slug
                let url = originalUrl |> ValueLens.TryCreate |> Result.value
                let cid = cid()
                use s = cqrsService.Sub.Subscribe((fun e -> e.CID = cid), 1) 
                let! slug = cqrsService.GenerateSlug cid url
                if slug.IsOk then
                    s.Task.Wait()
                
                let logger = ctx.GetLogger "SlugHandler"
                logger.LogInformation("Received URL: {Url}", url)
                let! res =  
                    cqrsService.Query<Query.Url>(filter = Predicate.Equal("OriginalUrl", originalUrl), take = 1) 
                    |> Async.map Seq.head

                return! json res.Slug next ctx
        }

let redirectHandler slug: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let cqrsService = ctx.GetService<Bootstrap.CQRSService>()
            match! cqrsService.Query<Query.Url>(filter = Predicate.Equal("Slug", slug), take = 1) with
            | [] -> return! setStatusCode 404 next ctx
            | [url] -> 
                let redirectUrl = ensureUrlHasProtocol url.OriginalUrl
                return! redirectTo false redirectUrl next ctx
            | _ -> return! setStatusCode 500 next ctx
        }
        
let webApp () : HttpHandler =
    choose [
        GET >=> route "/" >=> indexPageHandler
        POST >=> route "/api/slug" >=> slugHandler
        GET >=> routef "/%s" redirectHandler
    ]

let configureServices (services: IServiceCollection) =
    services
        .AddGiraffe()
        .AddSingleton<Environments.AppEnv>()
        .AddSingleton<Bootstrap.CQRSService>()
        .AddHostedService<Bootstrap.CQRSService>(fun p -> p.GetRequiredService<Bootstrap.CQRSService>())
        |> ignore

let configureLogging (ctx: WebHostBuilderContext) (logging: ILoggingBuilder) =
    if ctx.HostingEnvironment.IsDevelopment() then
        logging.ClearProviders().AddConsole().AddDebug() |> ignore
    else
        logging.ClearProviders().AddConsole() |> ignore

let configureAppConfiguration (ctx: WebHostBuilderContext) (config: IConfigurationBuilder) =
    if ctx.HostingEnvironment.IsDevelopment() then
        ctx.HostingEnvironment.ContentRootPath <- __SOURCE_DIRECTORY__

    config
        .SetBasePath(ctx.HostingEnvironment.ContentRootPath)
        .AddHoconFile("config.hocon")
        .AddHoconFile("secret.hocon", optional = true)
        .AddEnvironmentVariables()
    |> ignore

builder.WebHost
    .ConfigureLogging(configureLogging)
    .ConfigureAppConfiguration(configureAppConfiguration)
    .ConfigureServices(configureServices)
|> ignore

let private getClientIp (request: obj) : string =
    let proxyRequest = request :?> IIncomingHttpRequestProxy
    let httpContext = proxyRequest.Request.HttpContext
    let logger = httpContext.RequestServices.GetService<ILogger<obj>>() |> nonNull
    
    let ipAddress =
        match httpContext.Connection.RemoteIpAddress with
        | null -> 
            logger.LogWarning("RemoteIpAddress is null, using 'unknown_ip'")
            "unknown_ip"
        | addr ->
            let ip = addr.ToString()
            logger.LogInformation("Extracted IP address: {IpAddress}", ip)
            ip
    
    // Also log headers that might contain the real IP
    let headers = httpContext.Request.Headers
    if headers.ContainsKey("X-Forwarded-For") then
        logger.LogInformation("X-Forwarded-For header: {XForwardedFor}", headers.["X-Forwarded-For"].ToString())
    if headers.ContainsKey("X-Real-IP") then
        logger.LogInformation("X-Real-IP header: {XRealIP}", headers.["X-Real-IP"].ToString())
    if headers.ContainsKey("CF-Connecting-IP") then
        logger.LogInformation("CF-Connecting-IP header: {CFConnectingIP}", headers.["CF-Connecting-IP"].ToString())
    
    ipAddress

// Helper function to create rules for the slug API
let private createSlugApiRateLimitRule (limitMethod: ThrottlingTroll.RateLimitMethod) =
    ThrottlingTrollRule(
        UriPattern = "/api/slug", // Or "^/api/slug$" for exact regex match
        Method = "POST",
        LimitMethod = limitMethod,
        IdentityIdExtractor = getClientIp
    )

let configureApp (app: WebApplication) : WebApplication =
    app.UseStaticFiles()
        .UseThrottlingTroll(fun opts -> 
            let config = ThrottlingTrollConfig()

            config.Rules <- [|
                createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 150, IntervalInSeconds = 60));
                createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 300, IntervalInSeconds = 3600));
                createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 600, IntervalInSeconds = 86400));
            |]
            
            opts.Config <- config
        )
        .UseGiraffe(webApp ())
    app

let app = builder.Build() |> configureApp

app.Run()
