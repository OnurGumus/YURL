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
open Giraffe.EndpointRouting

#if DEBUG
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
#endif


let indexPageHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let contentRootPath = ctx.GetWebHostEnvironment().ContentRootPath
        let indexHtml = Path.Combine(contentRootPath, "templates", "index.html")

        task {
            let compiledHtml = init |> fromFile indexHtml
            return! htmlString compiledHtml next ctx
        }

let endpoints () = [
    GET [ route "/" indexPageHandler; routef "/%s" SlugHandler.redirectHandler ]
    POST [
        route "/api/slug" SlugHandler.slugHandler
        route "/api/report-abuse" AbuseReportHandler.handler
    ]
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

let builder = WebApplication.CreateBuilder()

builder.WebHost
    .ConfigureLogging(configureLogging)
    .ConfigureAppConfiguration(configureAppConfiguration)
    .ConfigureServices
    configureServices
|> ignore

let configureApp (app: WebApplication) =
    app.Environment.ApplicationName <- "yurl"

    app
        .UseStaticFiles()
        .UseThrottlingTroll(fun opts -> opts.Config <- Throttling.config)
        .UseRouting()
        .UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints ()))
    |> ignore

    app

let app = builder.Build() |> configureApp

app.Run()
