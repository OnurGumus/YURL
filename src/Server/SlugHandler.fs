module SlugHandler

open Giraffe
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open FCQRS.Model.Data
open FsToolkit.ErrorHandling
open System

[<CLIMutable>]
type UrlRequest = { Url: string }


let ensureUrlHasProtocol (url: string) =
    if url.StartsWith("http://") || url.StartsWith("https://") then
        url
    else
        "https://" + url

let cid () : CID =
    Guid.CreateVersion7().ToString() |> ValueLens.CreateAsResult |> Result.value

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
                let cid = cid ()
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

let redirectHandler slug : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let cqrsService = ctx.GetService<Bootstrap.CQRSService>()

            match! cqrsService.Query<Query.Url>(filter = Predicate.Equal("Slug", slug), take = 1) with
            | [] -> return! setStatusCode 404 next ctx
            | [ url ] ->
                let redirectUrl = ensureUrlHasProtocol url.OriginalUrl
                return! redirectTo false redirectUrl next ctx
            | _ -> return! setStatusCode 500 next ctx
        }
