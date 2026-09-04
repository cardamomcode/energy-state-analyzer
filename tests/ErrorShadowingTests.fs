module Energy.Tests.ErrorShadowingTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
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
        cases
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
            ))
    )
