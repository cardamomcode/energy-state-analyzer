module Energy.Tests.InversionTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Languages
open Energy.Tests.TestUtils

/// Check inversion detection and the wording and number of recommendations.
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

    let feedbackTests =
        [ "Python", Python.pythonLanguageAdapter, "python/inversion_feedback.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/inversionFeedback.ts"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/InversionFeedback.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/inversion_feedback.cpp"
          "C#", CSharp.cSharpLanguageAdapter, "csharp/InversionFeedback.cs" ]
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                sprintf "%s: exact depth, constructive advice and one finding per function" label,
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture language fixture
                            let violations = analyzeFixture source tree language fixture
                            assertThat (Energy.Core.TreeSitter.nodeHasError tree) (isFalse)

                            for name, expected in
                                [ "flaggedThreeLevels",
                                  "These conditions are nested 3 levels deep. Consider extracting a named operation to reduce how many conditions readers must track."
                                  "flaggedAlternativeBody",
                                  "These conditions are nested 3 levels deep. Consider extracting a named operation to reduce how many conditions readers must track."
                                  "flaggedFourLevels",
                                  "These conditions are nested 4 levels deep. Consider extracting a named operation to reduce how many conditions readers must track."
                                  "flaggedNestedElse",
                                  "These conditions are nested 3 levels deep. Consider extracting a named operation to reduce how many conditions readers must track."
                                  "flaggedFiveGuards",
                                  "These 5 nested conditions keep the main operation indented. Consider guard clauses, preserving the existing return values and fallthrough behavior."
                                  "flaggedImplicitFallthrough",
                                  "These 2 nested conditions keep the main operation indented. Consider guard clauses, preserving the existing return values and fallthrough behavior." ] do
                                let functionName =
                                    if language.Id = "csharp" then
                                        System.Char.ToUpperInvariant(name.[0]).ToString() + name.Substring(1)
                                    else
                                        name

                                let findings =
                                    findFunctionRange source (FunctionName functionName)
                                    |> violationsIn violations
                                    |> List.filter (fun violation -> violation.Type = Inversion)

                                assertThat (findings |> List.map _.Message) (isEqualTo [ expected ])
                        }
                    )
            ))

    testList (
        "Integration: inversion opportunities (real code examples)",
        fixtureTests
        @ feedbackTests
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
