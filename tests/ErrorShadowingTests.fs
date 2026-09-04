module Energy.Tests.ErrorShadowingTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Languages
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/error_shadowing.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/errorShadowing.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/ErrorShadowing.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/error_shadowing.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/error_shadowing.cpp" ]

    testList (
        "Integration: error handling shadows logic",
        (cases
         |> List.map (fun (label, language, fixture) ->
             testAsync (
                 (sprintf "%s: flags functions dominated by error handling only" label),
                 fun _ ->
                     toAsync (
                         task {
                             let! (source, tree) = parseFixture language fixture
                             let violations = analyzeFixture source tree language fixture

                             let shadow = findFunctionRange source (FunctionName "shadowedByError")
                             let clean = findFunctionRange source (FunctionName "cleanPath")

                             let hits range =
                                 violationsIn violations range |> List.filter (fun v -> v.Type = ErrorShadowing)

                             let highCount range =
                                 hits range |> List.filter (fun v -> v.Severity = High) |> List.length

                             // decision: assertValidPositions runs over the whole file's violations, not just
                             // this detector's, so a malformed position from one detector still fails CI.
                             assertThat (hits shadow |> List.length) (isGreaterOrEqual 1)
                             assertThat (highCount shadow) (isGreaterOrEqual 1)
                             assertThat (hits clean |> List.length) (isEqualTo 0)
                             assertValidPositions violations source
                         }
                     )
             )))
        @ [ testAsync (
                "does not flag functions without error handling when threshold is zero",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture Python.pythonLanguageAdapter "python/error_shadowing.py"

                            let options =
                                { defaultAnalyzeOptions with
                                    ErrorShadowing =
                                        { defaultErrorShadowingThresholds with
                                            Threshold = 0.0 } }

                            let violations =
                                { Source = source
                                  Tree = tree
                                  Language = Python.pythonLanguageAdapter
                                  FileName = "python/error_shadowing.py" }
                                |> analyzeWith options
                                |> _.Violations

                            let clean = findFunctionRange source (FunctionName "cleanPath")

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = ErrorShadowing)
                                 |> List.length)
                                (isEqualTo 0)

                            let strictOptions =
                                { options with
                                    ErrorShadowing =
                                        { options.ErrorShadowing with
                                            Threshold = 1.0 } }

                            let strictViolations =
                                { Source = source
                                  Tree = tree
                                  Language = Python.pythonLanguageAdapter
                                  FileName = "python/error_shadowing.py" }
                                |> analyzeWith strictOptions
                                |> _.Violations

                            let shadow = findFunctionRange source (FunctionName "shadowedByError")

                            assertThat
                                (violationsIn strictViolations shadow
                                 |> List.filter (fun v -> v.Type = ErrorShadowing)
                                 |> List.length)
                                (isEqualTo 0)
                        }
                    )
            ) ]
    )
