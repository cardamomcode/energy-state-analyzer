module Energy.Tests.CognitiveTests

open System.Threading.Tasks

open Fable.Core.JsInterop

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin
open Energy.Tests.TestUtils

// decision: runs the full detector pipeline (analyzeSource, the same entry point the CLI and the
// extension use) against realistic multi-function files in every ported language — mirrors
// cognitiveComplexity.test.ts. Each fixture carries a clean single-flat-if function (cognitive 1,
// never flagged), a 6-deep nesting (medium), and a 7-deep nesting (high). TypeScript/F#/Kotlin are
// exercised here as their language adapters were ported alongside the detector.

let tests =
    let cases =
        [ "Python", PYTHON, "python/cognitiveComplexity.py"
          "TypeScript", TYPESCRIPT, "typescript/cognitiveComplexity.ts"
          "F#", FSHARP, "fsharp/cognitiveComplexity.fs"
          "Kotlin", KOTLIN, "kotlin/cognitiveComplexity.kt" ]

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

                            let violations = analyzeSource sourceCode tree language fixture
                            assertValidPositions violations sourceCode

                            let clean = findFunctionRange sourceCode "cleanSimpleFunction"
                            let complex = findFunctionRange sourceCode "flaggedComplexFunction"
                            let severe = findFunctionRange sourceCode "flaggedSevereFunction"

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
