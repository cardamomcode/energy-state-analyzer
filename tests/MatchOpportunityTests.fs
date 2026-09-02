module Energy.Tests.MatchOpportunityTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/matchOpportunity.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/matchOpportunity.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/matchOpportunity.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/matchOpportunity.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/matchOpportunity.cpp" ]

    let languageCases =
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: flags a 3-way discriminated chain only" label),
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture language fixture
                            let violations = analyzeFixture source tree language fixture
                            assertValidPositions violations source
                            let clean = findFunctionRange source (FunctionName "cleanMixedConditions")
                            let chain = findFunctionRange source (FunctionName "flaggedThreeWayChain")

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = MatchOpportunity)
                                 |> List.length)
                                (isEqualTo 0)

                            assertThat
                                (violationsIn violations chain
                                 |> List.exists (fun v -> v.Type = MatchOpportunity && v.Message.Contains("status")))
                                isTrue
                        }
                    )
            ))

    testList (
        "Integration: match opportunities (real code examples)",
        languageCases
        @ [ testAsync (
                "C++: string and floating-point equality chains stay quiet",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) =
                                parseFixture CPlusPlus.cPlusPlusLanguageAdapter "cpp/matchOpportunity.cpp"

                            let violations =
                                analyzeFixture source tree CPlusPlus.cPlusPlusLanguageAdapter "cpp/matchOpportunity.cpp"

                            for name in [ "cleanStringChain"; "cleanFloatingChain" ] do
                                assertThat
                                    (violationsIn violations (findFunctionRange source (FunctionName name))
                                     |> List.filter (fun violation -> violation.Type = MatchOpportunity)
                                     |> List.length)
                                    (isEqualTo 0)
                        }
                    )
            ) ]
    )
