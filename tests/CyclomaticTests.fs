module Energy.Tests.CyclomaticTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Detectors.Cyclomatic
open Energy.Core.LanguageAdapter
open Energy.Languages
open Energy.Tests.TestUtils

// decision: runs the full detector pipeline (analyze, the same entry point the CLI and the
// extension use) against realistic multi-function files in every ported language — mirrors
// cyclomaticComplexity.test.ts. Each fixture carries a clean single-branch function (complexity 2,
// never flagged), an 11-way branch (medium), and a 16-way branch (high). TypeScript/F#/Kotlin are
// exercised here as their language adapters were ported alongside the detector.

let rec private functionNodes (language: LanguageAdapter) (node: Node) : Node list =
    if language.IsFunctionDefinition node then
        [ node ]
    else
        nodeChildren node |> List.collect (functionNodes language)

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/cyclomatic_complexity.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/cyclomaticComplexity.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/CyclomaticComplexity.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/CyclomaticComplexity.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/cyclomatic_complexity.cpp" ]

    let branchCases =
        [ "Python", Python.pythonLanguageAdapter, "python/cyclomatic_branches.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/cyclomaticBranches.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/CyclomaticBranches.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/CyclomaticBranches.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/cyclomatic_branches.cpp" ]

    let regressionTests =
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: a single branch stays clean, an 11-way branch is medium, a 16-way branch is high" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture language fixture

                            let violations = analyzeFixture sourceCode tree language fixture
                            assertValidPositions violations sourceCode

                            let clean = findFunctionRange sourceCode (FunctionName "cleanSimpleFunction")
                            let complex = findFunctionRange sourceCode (FunctionName "flaggedComplexFunction")
                            let severe = findFunctionRange sourceCode (FunctionName "flaggedSevereFunction")

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

    let branchTests =
        branchCases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: explicit and implicit third paths have McCabe complexity 3" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (_, tree) = parseFixture language fixture

                            let complexities = functionNodes language tree |> List.map (complexityOf language)
                            assertThat complexities (isEqualTo [ 3; 3 ])
                        }
                    ))
            ))

    testList ("Integration: cyclomatic complexity (real code examples)", regressionTests @ branchTests)
