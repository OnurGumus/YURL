module GenerateSlugFeature

open TickSpec
open Expecto
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open System.Net.Http
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Hosting

// Shared state for the test
type TestContext = {
    HttpClient: HttpClient
    GeneratedSlug: string option
}

[<Given>]
let ``the URL store is empty`` () =
    // Just a setup step, we'll create the context in the next step
    ()

[<Given>]
let ``no URL (.*) has been shortened yet`` (url: string) = ()
// Create and initialize the test context here


[<Given>]
let ``the page at "(.*)" returns HTML with title "(.*)"`` (url: string) (title: string) =
    (task {
        let builder = WebApplication.CreateBuilder()

        builder.WebHost
            .UseTestServer()
            .ConfigureLogging(Program.configureLogging)
            .ConfigureAppConfiguration(fun ctx configurationBuilder ->
                Program.configureAppConfiguration ctx configurationBuilder
                let path = System.IO.Path.GetTempFileName() + ".db"
                printfn "Using in-memory configuration file: %s" path
                let connectionString = $"Data Source={path}"
                System.Environment.SetEnvironmentVariable("OPENAI_API_KEY", "")
                configurationBuilder.AddInMemoryCollection([ 
                    "config:connection-string",  connectionString
                    "config:akka:persistence:journal:sql:connection-string",  connectionString
                    "config:akka:persistence:query:journal:sql:connection-string", connectionString
                    "config:akka:persistence:snapshot-store:sql:connection-string", connectionString
                    "config:config:openai-api-key", ""
                    "config:assistant-id", ""
                    ] |> Map.ofList)
                |> ignore)
            .ConfigureServices Program.configureServices
        |> ignore

        let app = builder.Build() |> Program.configureApp
        app.StartAsync().Wait()

        let handler = app.GetTestServer().CreateHandler()
        let httpClient = new HttpClient(handler)

        return {
            HttpClient = httpClient
            GeneratedSlug = None
        }
    })
        .Result

[<When>]
let ``I shorten the URL "(.*)"`` (url: string) (context: TestContext) =
    (task {
        // Create the request body
        let requestBody = sprintf """{"url": "%s"}""" url

        let content =
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")

        // Call the slug generation endpoint
        let! response = context.HttpClient.PostAsync("http://localhost/api/slug", content)
        response.EnsureSuccessStatusCode() |> ignore

        let! slugContent = response.Content.ReadAsStringAsync()
        // The response is a JSON string with quotes, we need to remove them
        let slug = slugContent.Trim('"')
        printfn "Generated slug: %s" slug

        // Return updated context with the generated slug
        return {
            context with
                GeneratedSlug = Some slug
        }
    })
        .Result

[<Then>]
let ``the system should create the slug "(.*)"`` (expectedSlug: string) (context: TestContext) =
    match context.GeneratedSlug with
    | None -> failwith "No slug was generated"
    | Some slug ->
       
            // Otherwise check for the specific slug
            Expect.equal slug expectedSlug "The generated slug should match the expected slug"

    context

[<Then>]
let ``navigating to "(.*)" should redirect to "(.*)"`` (slug: string) (url: string) (context: TestContext) =
    (task {
        let! response = context.HttpClient.GetAsync(sprintf "http://localhost%s" slug)
        
        // Check that we get a redirect status code (301 or 302)
        let isRedirect = 
            response.StatusCode = System.Net.HttpStatusCode.Redirect ||
            response.StatusCode = System.Net.HttpStatusCode.MovedPermanently
        Expect.isTrue isRedirect "Should return a redirect status code (301 or 302)"
        
        // Check that the Location header contains the correct URL
        let locationHeader = response.Headers.Location 
        Expect.isNotNull locationHeader "Location header should be present"
        let locationHeader = locationHeader |> nonNull
        Expect.equal (locationHeader.ToString()) url "Should redirect to the correct URL"
    }).Wait()

    context
