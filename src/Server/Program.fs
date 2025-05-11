open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Giraffe
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http

let builder= WebApplication.CreateBuilder()

let rootHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            return! text "Hello World" next ctx
        }

builder.Services.AddGiraffe() |> ignore

builder.WebHost.ConfigureLogging(fun logging ->
    logging.ClearProviders().AddConsole().AddDebug() |> ignore
) |> ignore
let app = builder.Build()

app.UseGiraffe(rootHandler)
app.Run()
