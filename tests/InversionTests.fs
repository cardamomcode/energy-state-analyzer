module Energy.Tests.InversionTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin
open Energy.Languages.CPlusPlus
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", PYTHON, "python/inversion.py"
          "TypeScript", TYPESCRIPT, "typescript/inversion.ts"
          "Kotlin", KOTLIN, "kotlin/inversion.kt"
          "C++", CPP, "cpp/inversion.cpp" ]

    let fixtureTests =
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: flags dominant blocks and validation chains" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture language fixture
                            let violations = analyzeSource source tree language fixture
                            assertValidPositions violations source
                            let clean = findFunctionRange source "cleanEarlyReturn"
                            let dominant = findFunctionRange source "flaggedDominantIf"
                            let chain = findFunctionRange source "flaggedValidationChain"

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
                            let! (source, tree) = parseFixture FSHARP "fsharp/inversion.fs"
                            let violations = analyzeSource source tree FSHARP "inversion.fs"

                            assertThat
                                (violations |> List.filter (fun v -> v.Type = Inversion) |> List.length)
                                (isEqualTo 0)
                        }
                    ))
            ) ]
    )
