module Energy.Tests.ParameterCountTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages
open Energy.Tests.TestUtils

// decision: runs the complete pipeline against the existing real-code fixtures, rather than calling
// the detector in isolation. This preserves the integration contract shared by the CLI and extension
// while proving parameter shapes are recognized consistently in every supported grammar.

// decision: proves the detector reads its thresholds from ctx.Options rather than hardcoding them —
// loosening the medium threshold past the 6-param example clears it, while the high threshold still
// fires on the 9-param example. Without the ctx.Options read this test could not pass.
let configOverrideTests =
    testList (
        "Integration: parameter count respects configured thresholds",
        [ testAsync (
              "a raised medium threshold clears the 6-param function while high still fires at 9",
              fun _ ->
                  toAsync (
                      task {
                          let! (_sourceCode, tree) =
                              parseFixture TypeScript.typeScriptLanguageAdapter "typescript/parameterCount.ts"

                          let custom =
                              { defaultThresholds with
                                  ParameterCount =
                                      { Enabled = true
                                        MediumThreshold = 7
                                        HighThreshold = 8 } }

                          let ctx =
                              createTestContext
                                  _sourceCode
                                  tree
                                  TypeScript.typeScriptLanguageAdapter
                                  "typescript/parameterCount.ts"
                                  custom

                          let violations = runPipeline ctx
                          assertValidPositions violations _sourceCode

                          let many = findFunctionRange _sourceCode (FunctionName "flaggedManyParams")
                          let tooMany = findFunctionRange _sourceCode (FunctionName "flaggedTooManyParams")

                          // 6 params is no longer past the raised medium threshold of 7.
                          assertThat
                              (violationsIn violations many
                               |> List.filter (fun v -> v.Type = Parameters)
                               |> List.length)
                              (isEqualTo 0)

                          // 9 params still exceeds the high threshold of 8.
                          let tooManyHits =
                              violationsIn violations tooMany |> List.filter (fun v -> v.Type = Parameters)

                          assertThat (tooManyHits.Length > 0) isTrue
                          assertThat (List.head tooManyHits).Severity (isEqualTo High)
                      }
                  )
          ) ]
    )

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/parameter_count.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/parameterCount.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/ParameterCount.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/ParameterCount.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/parameter_count.cpp" ]

    testList (
        "Integration: parameter count (real code examples)",
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: 2 params stays clean, 6 params is medium, 9 params is high" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture language fixture

                            let violations = analyzeFixture sourceCode tree language fixture
                            assertValidPositions violations sourceCode

                            let clean = findFunctionRange sourceCode (FunctionName "cleanFewParams")
                            let many = findFunctionRange sourceCode (FunctionName "flaggedManyParams")
                            let tooMany = findFunctionRange sourceCode (FunctionName "flaggedTooManyParams")

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = Parameters)
                                 |> List.length)
                                (isEqualTo 0)

                            let manyHits =
                                violationsIn violations many |> List.filter (fun v -> v.Type = Parameters)

                            assertThat (manyHits.Length > 0) isTrue
                            assertThat (List.head manyHits).Severity (isEqualTo Medium)

                            let tooManyHits =
                                violationsIn violations tooMany |> List.filter (fun v -> v.Type = Parameters)

                            assertThat (tooManyHits.Length > 0) isTrue
                            assertThat (List.head tooManyHits).Severity (isEqualTo High)
                        }
                    ))
            ))
    )
