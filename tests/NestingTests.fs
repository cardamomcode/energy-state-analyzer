module Energy.Tests.NestingTests

open System.Threading.Tasks

open Fable.Core.JsInterop

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages.Python
open Energy.Tests.TestUtils

// decision: runs the full detector pipeline (analyzeSource, the same entry point the CLI and the
// extension use) against a realistic multi-function file, rather than calling analyzeNesting in
// isolation — mirrors nesting.test.ts, whose unit suite already covers the detector's internals with
// single-line synthetic snippets. Only Python is exercised here; TypeScript/F#/Kotlin share this
// fixture and are added as their language adapters are ported (later batches).

let tests =
    testList (
        "Integration: nesting (Python)",
        [
            testAsync (
                "shallow stays clean, deep is medium, severe is high, try-nesting flagged",
                (fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture PYTHON "python/nesting.py"

                            let violations = analyzeSource source tree PYTHON "python/nesting.py"
                            assertValidPositions violations source

                            let clean = findFunctionRange source "cleanShallowNesting"
                            let deep = findFunctionRange source "flaggedDeepNesting"
                            let severe = findFunctionRange source "flaggedSevereNesting"
                            let tryNested = findFunctionRange source "flaggedTryNesting"

                            // shallow (2-level) nesting should not be flagged.
                            assertThat
                                (List.length (violationsIn violations clean |> List.filter (fun v -> v.Type = Nesting)))
                                (isEqualTo 0)

                            // 5-level-deep function: at least one violation, the first medium.
                            let deepNesting = violationsIn violations deep |> List.filter (fun v -> v.Type = Nesting)

                            assertThat (List.length deepNesting > 0) isTrue
                            assertThat (List.head deepNesting).Severity (isEqualTo Medium)

                            // 7-level-deep function: at least one violation, the last high.
                            let severeNesting = violationsIn violations severe |> List.filter (fun v -> v.Type = Nesting)

                            assertThat (List.length severeNesting > 0) isTrue
                            assertThat (List.last severeNesting).Severity (isEqualTo High)

                            // 5-level-deep try/catch: at least one violation.
                            let tryNesting = violationsIn violations tryNested |> List.filter (fun v -> v.Type = Nesting)

                            assertThat (List.length tryNesting > 0) isTrue
                        }
                    ))
            )
        ]
    )
