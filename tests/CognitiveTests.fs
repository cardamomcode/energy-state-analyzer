module Energy.Tests.CognitiveTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Languages
open Energy.Tests.TestUtils

// decision: runs the full detector pipeline (analyze, the same entry point the CLI and the
// extension use) against realistic multi-function files in every ported language — mirrors
// cognitiveComplexity.test.ts. Each fixture carries a clean single-flat-if function (cognitive 1,
// never flagged), a 6-deep nesting (medium), and a 7-deep nesting (high). TypeScript/F#/Kotlin are
// exercised here as their language adapters were ported alongside the detector.

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/cognitiveComplexity.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/cognitiveComplexity.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/cognitiveComplexity.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/cognitiveComplexity.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/cognitiveComplexity.cpp" ]

    testList (
        "Integration: cognitive complexity (real code examples)",
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: a flat check stays clean, 6-deep nesting is medium, 7-deep nesting is high" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture language fixture

                            let violations = analyzeFixture sourceCode tree language fixture
                            assertValidPositions violations sourceCode

                            let clean = findFunctionRange sourceCode (FunctionName "cleanSimpleFunction")
                            let complex = findFunctionRange sourceCode (FunctionName "flaggedComplexFunction")
                            let severe = findFunctionRange sourceCode (FunctionName "flaggedSevereFunction")

                            // a single flat if should not be flagged.
                            assertThat
                                (List.length (
                                    violationsIn violations clean |> List.filter (fun v -> v.Type = Cognitive)
                                ))
                                (isEqualTo 0)

                            let complexHit =
                                violationsIn violations complex |> List.filter (fun v -> v.Type = Cognitive)

                            assertThat (List.length complexHit > 0) isTrue
                            assertThat (List.head complexHit).Severity (isEqualTo Medium)

                            let severeHit =
                                violationsIn violations severe |> List.filter (fun v -> v.Type = Cognitive)

                            assertThat (List.length severeHit > 0) isTrue
                            assertThat (List.last severeHit).Severity (isEqualTo High)
                        }
                    ))
            ))
    )
