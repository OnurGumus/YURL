module Firm.Workflow.IntegrationTest.Program

open System.Reflection
open TickSpec
open Expecto
open System.Runtime.ExceptionServices

let assembly = Assembly.GetExecutingAssembly()
let stepDefinitions = StepDefinitions assembly

let featureFromEmbeddedResource fullResourceName =
    match assembly.GetManifestResourceStream fullResourceName with
    | null -> failwithf "Feature file %s not found as embedded resource." fullResourceName
    | stream ->
        use s = stream
        stepDefinitions.GenerateFeature(fullResourceName, s)

let testListFromFeature feature =
    let scenarios =
        feature.Scenarios
        |> Seq.map (fun scenario ->

            let testCaseFunc =
                if scenario.Name.TrimStart().StartsWith "_" then
                    ftestCase
                else
                    testCase

            testCaseFunc scenario.Name
            <| fun () ->
                try
                    scenario.Action.Invoke()
                with
                | :? TargetInvocationException as ex when ex.InnerException <> null ->
                    // throw the actual exception to get the assertion failure
                    ex.InnerException 
                    |> nonNull
                    |> ExceptionDispatchInfo.Capture 
                     |> _.Throw()
                | _ -> reraise ())
        |> Seq.toList

    let featureTestListFunc =
        if feature.Name.TrimStart().StartsWith "_" then
            ftestList
        else
            testList

    featureTestListFunc feature.Name scenarios

let createFeatureTest baseFeatureName =
    $"IntegrationTest.{baseFeatureName}.feature"
    |> featureFromEmbeddedResource
    |> testListFromFeature

[<Tests>]
let tests =
    [ "GenerateSlug" ]
    |> List.map createFeatureTest
    |> testList "Tests"
    |> testSequenced

[<EntryPoint>]
let main argv =
    runTestsInAssemblyWithCLIArgs [ Sequenced ] argv