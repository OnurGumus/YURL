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

[<CLIMutable>]
type UrlRequest = { Url: string }
let cid (): CID =
    System.Guid.NewGuid().ToString() |> ValueLens.CreateAsResult |> Result.value
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
            let cqrsService = ctx.GetService<Bootstrap.CQRSService>()
            let url = url.Url |>ValueLens.TryCreate |> Result.value
            let cid = cid()
            let s = cqrsService.Sub.Subscribe((fun e -> e.CID = cid), 1) |> Async.StartAsTask
            let! slug = cqrsService.GenerateSlug cid url
            use! result = s
            let logger = ctx.GetLogger "SlugHandler"
            logger.LogInformation("Received URL: {Url}", url)

            let guid = Guid.NewGuid().ToString()
            return! json guid next ctx
        }

let webApp () : HttpHandler =
    choose [
        GET >=> route "/" >=> indexPageHandler
        POST >=> route "/api/slug" >=> slugHandler
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
        .AddEnvironmentVariables()
    |> ignore

builder.WebHost
    .ConfigureLogging(configureLogging)
    .ConfigureAppConfiguration(configureAppConfiguration)
    .ConfigureServices(configureServices)
|> ignore

let private getClientIp (request: obj) : string =
    printfn "IPoo"
    let proxyRequest = request :?> IIncomingHttpRequestProxy
    match proxyRequest.Request.HttpContext.Connection.RemoteIpAddress with
    | null -> "unknown_ip"
    | addr ->
        let ip = addr.ToString()
        printfn "IP: %s" ip
        ip

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
                createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 2, IntervalInSeconds = 60));
                createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 5, IntervalInSeconds = 60));
                createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 10, IntervalInSeconds = 3600));
                createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 20, IntervalInSeconds = 86400));
            |]
            
            opts.Config <- config
        )
        .UseGiraffe(webApp ())
    app

let app = builder.Build() |> configureApp

app.Run()
