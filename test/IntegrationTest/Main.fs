module Firm.Workflow.IntegrationTest.Main

open System.Reflection
open TickSpec
open Expecto

let executeFeature (featureName: string) =
    let assembly = Assembly.GetExecutingAssembly()
    let definitions = StepDefinitions assembly

    let stream =
        assembly.GetManifestResourceStream $"IntegrationTest.{featureName}.feature"

    match stream with
    | null -> failwithf "Feature file %s.feature not found" featureName
    | s ->
        try
            definitions.Execute(featureName, s)
        with
        | :? TargetInvocationException as ex when ex.InnerException <> null ->
            // Re-throw the inner exception to get the actual test failure
            raise (nonNull ex.InnerException)
        | ex -> raise ex

let runFeatureTest featureName =
    testCaseAsync
        featureName
        (async {
            try
                executeFeature featureName
            with
            | :? Expecto.AssertException as ex ->
                // Re-throw with better context
                failtestf "Test failed: %s" <| ex.ToString()
            | ex ->
                // Re-throw other exceptions with better context
                failtestf "Test failed with unexpected error: %s" <| ex.ToString()
        })

[<Tests>]
let tests = 
    testSequenced 
    <| testList "Integration Tests" [ runFeatureTest "GenerateSlug" ]
