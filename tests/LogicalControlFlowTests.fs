module Energy.Tests.LogicalControlFlowTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/logical_control_flow.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/logicalControlFlow.ts"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/logical_control_flow.cpp" ]

    testList (
        "Integration: logical control flow",
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: explicit if stays clean" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture language fixture
                            let violations = analyzeFixture source tree language fixture
                            let clean = findFunctionRange source (FunctionName "cleanExplicitIf")
                            let andIf = findFunctionRange source (FunctionName "flaggedAndAsIf")
                            let orIf = findFunctionRange source (FunctionName "flaggedOrAsUnless")

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = LogicalControlFlow)
                                 |> List.length)
                                (isEqualTo 0)

                            assertThat
                                (violationsIn violations andIf
                                 |> List.exists (fun v -> v.Type = LogicalControlFlow && v.Severity = Low))
                                isTrue

                            assertThat
                                (violationsIn violations orIf
                                 |> List.exists (fun v -> v.Type = LogicalControlFlow && v.Severity = Low))
                                isTrue
                        }
                    ))
            ))
    )
