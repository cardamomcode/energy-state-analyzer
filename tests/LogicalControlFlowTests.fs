module Energy.Tests.LogicalControlFlowTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.CPlusPlus
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", PYTHON, "python/logicalControlFlow.py"
          "TypeScript", TYPESCRIPT, "typescript/logicalControlFlow.ts"
          "C++", CPP, "cpp/logicalControlFlow.cpp" ]

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
                            let violations = analyzeSource source tree language fixture
                            let clean = findFunctionRange source "cleanExplicitIf"
                            let andIf = findFunctionRange source "flaggedAndAsIf"
                            let orIf = findFunctionRange source "flaggedOrAsUnless"

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
