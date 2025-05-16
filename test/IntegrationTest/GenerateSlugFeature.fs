module GenerateSlugFeature

open TickSpec
open Expecto
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open System.Net.Http
open Giraffe
open System.IO

open  Microsoft.AspNetCore.Hosting;
[<Given>]
let ``the URL store is empty`` () =
    ()
        
[<Given>]
let ``no URL (.*) has been shortened yet`` (url: string) =
    ()

[<Given>]
let ``the page at (.*) returns HTML with title (.*)`` (url: string) (title: string) =
    (task{
        let builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer()
                .ConfigureLogging(Program.configureLogging)
                .ConfigureAppConfiguration(Program.configureAppConfiguration) 
                .ConfigureServices(Program.configureServices)
                |> ignore

        let app = builder.Build() |> Program.configureApp
        
        do! app.StartAsync()

        let handler = app.GetTestServer().CreateHandler()
        let httpClient = new HttpClient(handler)
        let! response = httpClient.GetAsync("http://localhost/") 
        printfn "response: %A" response.StatusCode
        let! content = response.Content.ReadAsStringAsync()
        printfn "content: %A" content
        return ()
    }) .Result

    


[<When>]
let ``I shorten the URL (.*)`` (url: string) =
    ()


[<Then>]
let ``the system should create the slug (.*)`` (slug: string) =
    ()


[<Then>]
let ``navigating to (.*) should redirect to (.*)`` (slug: string) (url: string) =
    ()







