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

let builder= WebApplication.CreateBuilder()

let indexPageHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let contentRootPath =  ctx.GetWebHostEnvironment().ContentRootPath
        let indexHtml = Path.Combine(contentRootPath, "templates", "index.html")
        task {
            let compiledHtml =
                init
                |> add "someValue" "myValue"
                |> fromFile indexHtml
            return! htmlString compiledHtml next ctx
        }

let slugHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let guid = System.Guid.NewGuid().ToString()
            return! json guid next ctx
        }

let webApp: HttpHandler =
    choose [
        GET >=> route "/" >=> indexPageHandler
        GET >=> route "/api/slug" >=> slugHandler
    ]

let configureServices (services: IServiceCollection ) =
        services.AddGiraffe() |> ignore

let configureLogging (ctx: WebHostBuilderContext) (logging: ILoggingBuilder) =
    if ctx.HostingEnvironment.IsDevelopment() then
        logging.ClearProviders().AddConsole().AddDebug() |> ignore
    else
        logging.ClearProviders().AddConsole() |> ignore

let configureAppConfiguration (ctx: WebHostBuilderContext) (config: IConfigurationBuilder) =
    if ctx.HostingEnvironment.IsDevelopment() then
        ctx.HostingEnvironment.ContentRootPath  <- __SOURCE_DIRECTORY__
    
    config
        .SetBasePath(ctx.HostingEnvironment.ContentRootPath)
        .AddEnvironmentVariables()
        |> ignore

builder.WebHost
    .ConfigureLogging(configureLogging)
    .ConfigureAppConfiguration(configureAppConfiguration)
    .ConfigureServices(configureServices)
    |> ignore

let configureApp (app: WebApplication) =
        app
            .UseStaticFiles()
            .UseGiraffe webApp
        app
    
let app = 
    builder.Build() 
    |> configureApp

app.Run()

