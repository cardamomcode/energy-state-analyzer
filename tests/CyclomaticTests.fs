module Energy.Tests.CyclomaticTests

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
// cyclomaticComplexity.test.ts. Each fixture carries a clean single-branch function (complexity 2,
// never flagged), an 11-way branch (medium), and a 16-way branch (high). TypeScript/F#/Kotlin are
// exercised here as their language adapters were ported alongside the detector.

let tests =
    let cases =
        [ "Python", PYTHON, "python/cyclomaticComplexity.py"
          "TypeScript", TYPESCRIPT, "typescript/cyclomaticComplexity.ts"
          "F#", FSHARP, "fsharp/cyclomaticComplexity.fs"
          "Kotlin", KOTLIN, "kotlin/cyclomaticComplexity.kt" ]

    testList (
        "Integration: cyclomatic complexity (real code examples)",
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: a single branch stays clean, an 11-way branch is medium, a 16-way branch is high" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture language fixture

                            let violations = analyzeSource sourceCode tree language fixture
                            assertValidPositions violations sourceCode

                            let clean = findFunctionRange sourceCode "cleanSimpleFunction"
                            let complex = findFunctionRange sourceCode "flaggedComplexFunction"
                            let severe = findFunctionRange sourceCode "flaggedSevereFunction"

                            // a single if/else (complexity 2) should not be flagged.
                            assertThat
                                (List.length (
                                    violationsIn violations clean |> List.filter (fun v -> v.Type = Complexity)
                                ))
                                (isEqualTo 0)

                            let complexHit =
                                violationsIn violations complex |> List.filter (fun v -> v.Type = Complexity)

                            assertThat (List.length complexHit > 0) isTrue
                            assertThat (List.head complexHit).Severity (isEqualTo Medium)

                            let severeHit =
                                violationsIn violations severe |> List.filter (fun v -> v.Type = Complexity)

                            assertThat (List.length severeHit > 0) isTrue
                            assertThat (List.last severeHit).Severity (isEqualTo High)
                        }
                    ))
            ))
    )
