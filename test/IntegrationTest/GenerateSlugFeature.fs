module GenerateSlugFeature

open TickSpec
open Expecto


[<Given>]
let ``the URL store is empty`` () =
    ()
        
[<Given>]
let ``no URL (.*) has been shortened yet`` (url: string) =
    ()

[<Given>]
let ``the page at (.*) returns HTML with title (.*)`` (url: string) (title: string) =
    ()


[<When>]
let ``I shorten the URL (.*)`` (url: string) =
    ()


[<Then>]
let ``the system should create the slug (.*)`` (slug: string) =
    ()


[<Then>]
let ``navigating to (.*) should redirect to (.*)`` (slug: string) (url: string) =
    ()







