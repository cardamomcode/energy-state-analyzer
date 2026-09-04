module Energy.Tests.InversionTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Languages
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/inversion.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/inversion.ts"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/Inversion.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/inversion.cpp" ]

    let fixtureTests =
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: flags dominant blocks and validation chains" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture language fixture
                            let violations = analyzeFixture source tree language fixture
                            assertValidPositions violations source
                            let clean = findFunctionRange source (FunctionName "cleanEarlyReturn")
                            let dominant = findFunctionRange source (FunctionName "flaggedDominantIf")
                            let chain = findFunctionRange source (FunctionName "flaggedValidationChain")

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = Inversion)
                                 |> List.length)
                                (isEqualTo 0)

                            assertThat
                                (violationsIn violations dominant |> List.exists (fun v -> v.Type = Inversion))
                                isTrue

                            assertThat
                                (violationsIn violations chain |> List.exists (fun v -> v.Type = Inversion))
                                isTrue
                        }
                    ))
            ))

    testList (
        "Integration: inversion opportunities (real code examples)",
        fixtureTests
        @ [ testAsync (
                "F#: documented blockless grammar limitation remains quiet",
                (fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture FSharp.fSharpLanguageAdapter "fsharp/Inversion.fs"

                            let violations =
                                analyzeFixture source tree FSharp.fSharpLanguageAdapter "Inversion.fs"

                            assertThat
                                (violations |> List.filter (fun v -> v.Type = Inversion) |> List.length)
                                (isEqualTo 0)
                        }
                    ))
            ) ]
    )
