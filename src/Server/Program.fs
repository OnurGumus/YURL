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

let builder= WebApplication.CreateBuilder()

let rootHandler: HttpHandler =
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

let configureServices (services: IServiceCollection ) =
        services.AddGiraffe() |> ignore

let configureLogging (ctx: WebHostBuilderContext) (logging: ILoggingBuilder) =
    if ctx.HostingEnvironment.IsDevelopment() then
        logging.ClearProviders().AddConsole().AddDebug() |> ignore
    else
        logging.ClearProviders().AddConsole() |> ignore

let configureAppConfiguration (ctx: WebHostBuilderContext) (config: IConfigurationBuilder) =
    config.SetBasePath(ctx.HostingEnvironment.ContentRootPath)
        .AddEnvironmentVariables()
    |> ignore

builder.WebHost
    .ConfigureLogging(configureLogging)
    .ConfigureAppConfiguration(configureAppConfiguration)
    .ConfigureServices(configureServices)
    .Build()
    |> ignore

let configureApp (app: WebApplication) =
        rootHandler |> app.UseGiraffe
        app
    
let app = 
    builder.Build() 
    |> configureApp

app.Run()

