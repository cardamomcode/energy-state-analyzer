module Energy.Tests.NestingTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Languages
open Energy.Tests.TestUtils

// decision: runs the full detector pipeline (analyze, the same entry point the CLI and the
// extension use) against a realistic multi-function file, rather than calling analyzeNesting in
// isolation — mirrors nesting.test.ts, whose unit suite already covers the detector's internals with
// single-line synthetic snippets.

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/nesting.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/nesting.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/Nesting.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/Nesting.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/nesting.cpp" ]

    testList (
        "Integration: nesting (real code examples)",
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                sprintf "%s: shallow stays clean, deep is medium, severe is high, try-nesting flagged" label,
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture language fixture

                            let violations =
                                analyze
                                    { Source = source
                                      Tree = tree
                                      Language = language
                                      FileName = fixture }
                                |> _.Violations

                            assertValidPositions violations source

                            let nestingHits name =
                                violationsIn violations (findFunctionRange source (FunctionName name))
                                |> List.filter (fun v -> v.Type = Nesting)

                            assertThat (nestingHits "cleanShallowNesting" |> List.length) (isEqualTo 0)

                            let deep = nestingHits "flaggedDeepNesting"
                            assertThat (List.length deep > 0) isTrue
                            assertThat (List.head deep).Severity (isEqualTo Medium)

                            let severe = nestingHits "flaggedSevereNesting"
                            assertThat (List.length severe > 0) isTrue
                            assertThat (List.last severe).Severity (isEqualTo High)

                            assertThat (nestingHits "flaggedTryNesting" |> List.length > 0) isTrue
                        }
                    )
            ))
    )

// decision: disabling a detector must skip it entirely while leaving the rest of the pipeline
// intact — so an editor toggle turns one detector off without silently muting its neighbours.
let gatingTests =
    let gatingCases =
        [ "Python", Python.pythonLanguageAdapter, "python/nesting.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/nesting.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/Nesting.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/Nesting.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/nesting.cpp" ]

    testList (
        "Integration: nesting enable/disable gating",
        gatingCases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                sprintf "%s: disabling the nesting detector removes its violations only" label,
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture language fixture

                            let input =
                                { Source = source
                                  Tree = tree
                                  Language = language
                                  FileName = fixture }

                            let disabledOpts =
                                { defaultAnalyzeOptions with
                                    Nesting =
                                        { defaultNestingThresholds with
                                            Enabled = false } }

                            let enabledViolations = input |> analyze |> _.Violations

                            let disabledViolations = input |> analyzeWith disabledOpts |> _.Violations

                            // the detector is on by default, so it produces nesting findings...
                            assertThat
                                (enabledViolations |> List.filter (fun v -> v.Type = Nesting) |> List.length > 0)
                                isTrue
                            // ...and toggling it off removes exactly those, with nothing else changing: the disabled
                            // run equals the enabled run with only the nesting violations removed.
                            let enabledWithoutNesting =
                                enabledViolations |> List.filter (fun v -> v.Type <> Nesting)

                            assertThat
                                (disabledViolations |> List.filter (fun v -> v.Type = Nesting) |> List.length)
                                (isEqualTo 0)

                            assertThat disabledViolations (isEqualTo enabledWithoutNesting)
                        }
                    )
            ))
    )
