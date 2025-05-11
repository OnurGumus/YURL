open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Giraffe
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open Fue.Data
open Fue.Compiler
open System.IO
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

builder.Services.AddGiraffe() |> ignore

builder.WebHost.ConfigureLogging(fun ctx logging ->
    if ctx.HostingEnvironment.IsDevelopment() then
        logging.ClearProviders().AddConsole().AddDebug() |> ignore
    else
        logging.ClearProviders().AddConsole() |> ignore
).ConfigureAppConfiguration(fun ctx _ -> 
        ctx.HostingEnvironment.ApplicationName <- "tyni.ai" 
) |> ignore

let app = builder.Build()

rootHandler |> app.UseGiraffe
app.Run()
