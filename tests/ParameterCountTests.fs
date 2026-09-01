module Energy.Tests.ParameterCountTests

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

// decision: runs the complete pipeline against the existing real-code fixtures, rather than calling
// the detector in isolation. This preserves the integration contract shared by the CLI and extension
// while proving parameter shapes are recognized consistently in every supported grammar.
let tests =
    let cases =
        [ "Python", PYTHON, "python/parameterCount.py"
          "TypeScript", TYPESCRIPT, "typescript/parameterCount.ts"
          "F#", FSHARP, "fsharp/parameterCount.fs"
          "Kotlin", KOTLIN, "kotlin/parameterCount.kt"
          "C++", CPP, "cpp/parameterCount.cpp" ]

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
