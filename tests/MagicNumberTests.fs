module Energy.Tests.MagicNumberTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Detectors.MagicNumber
open Energy.Core.Position
open Energy.Languages
open Energy.Tests.TestUtils

// decision: runs the pipeline over the established real-code fixtures so the detector's grammar
// hooks, position mapping, and registration are exercised together for every supported language.
let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/magic_number.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/magicNumber.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/MagicNumber.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/MagicNumber.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/magic_number.cpp" ]

    let fixtureTests =
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: significant literals are flagged once" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture language fixture
                            let violations = analyzeFixture sourceCode tree language fixture
                            assertValidPositions violations sourceCode

                            let clean = findFunctionRange sourceCode (FunctionName "cleanCommonValues")
                            let numbers = findFunctionRange sourceCode (FunctionName "flaggedMagicNumbers")
                            let negative = findFunctionRange sourceCode (FunctionName "cleanNegativeValue")

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)

                            assertThat
                                (violationsIn violations negative
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)

                            assertThat
                                (violationsIn violations numbers
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 3)
                        }
                    ))
            ))

    testList (
        "Integration: magic numbers (real code examples)",
        fixtureTests
        @ [ testAsync (
                "Python: module constants, index literals, and defaults are exempt",
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) =
                                parseFixture Python.pythonLanguageAdapter "python/magic_number.py"

                            let violations =
                                analyzeFixture sourceCode tree Python.pythonLanguageAdapter "magic_number.py"

                            let exempt = findFunctionRange sourceCode (FunctionName "exemptIndexAndDefault")

                            assertThat
                                (violations |> List.filter (fun v -> v.Line = 0 && v.Type = Magic) |> List.length)
                                (isEqualTo 0)

                            assertThat
                                (violationsIn violations exempt
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)
                        }
                    ))
            )
            testAsync (
                "Kotlin: explicit constants are exempt at every nesting depth",
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture Kotlin.kotlinLanguageAdapter "kotlin/MagicNumber.kt"

                            let violations =
                                analyzeFixture sourceCode tree Kotlin.kotlinLanguageAdapter "magic_number.kt"

                            let limits = findFunctionRange sourceCode (FunctionName "Limits")

                            assertThat
                                (violationsIn violations limits
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)
                        }
                    ))
            )
            testAsync (
                "C++: constexpr declarations and enumerators are exempt at every nesting depth",
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) =
                                parseFixture CPlusPlus.cPlusPlusLanguageAdapter "cpp/magic_number.cpp"

                            let violations =
                                analyzeFixture sourceCode tree CPlusPlus.cPlusPlusLanguageAdapter "magic_number.cpp"

                            let limits = findFunctionRange sourceCode (FunctionName "Limits")

                            assertThat
                                (violationsIn violations limits
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)
                        }
                    ))
            )
            testAsync (
                "F# and annotated Kotlin module bindings are constant contexts",
                (fun _ ->
                    toAsync (
                        task {
                            let! (fsharpSource, fsharpTree) =
                                parseFixture FSharp.fSharpLanguageAdapter "fsharp/MagicNumber.fs"

                            let fsharpViolations =
                                analyzeFixture fsharpSource fsharpTree FSharp.fSharpLanguageAdapter "magic_number.fs"

                            let maxRetries = findFunctionRange fsharpSource (FunctionName "maxRetries")

                            let! (kotlinSource, kotlinTree) =
                                parseFixture Kotlin.kotlinLanguageAdapter "kotlin/MagicNumber.kt"

                            let kotlinViolations =
                                analyzeFixture kotlinSource kotlinTree Kotlin.kotlinLanguageAdapter "magic_number.kt"

                            let annotatedRetries =
                                findFunctionRange kotlinSource (FunctionName "MAX_ANNOTATED_RETRIES")

                            assertThat
                                (violationsIn fsharpViolations maxRetries
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)

                            assertThat
                                (violationsIn kotlinViolations annotatedRetries
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)
                        }
                    ))
            )
            testAsync (
                "options and test-file names suppress magic-number findings only when intended",
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) =
                                parseFixture Python.pythonLanguageAdapter "python/magic_number.py"

                            let positions = createPositionLookup sourceCode
                            let numbers = findFunctionRange sourceCode (FunctionName "flaggedMagicNumbers")

                            let disabled =
                                createTestContext
                                    sourceCode
                                    tree
                                    Python.pythonLanguageAdapter
                                    "magic_number.py"
                                    { defaultThresholds with
                                        MagicNumber =
                                            { Enabled = false
                                              Allowlist = []
                                              IncludeTestFiles = false } }
                                |> analyzeMagicNumbers
                                |> _.Violations

                            let customAllowlist =
                                createTestContext
                                    sourceCode
                                    tree
                                    Python.pythonLanguageAdapter
                                    "magic_number.py"
                                    { defaultThresholds with
                                        MagicNumber =
                                            { Enabled = true
                                              Allowlist = [ 0.0; 1.0; -1.0; 2.0; 1.08; 50.0; 15.75 ]
                                              IncludeTestFiles = false } }
                                |> analyzeMagicNumbers
                                |> _.Violations

                            let testFile =
                                analyzeFixture sourceCode tree Python.pythonLanguageAdapter "PricingTest.py"

                            let latestFile =
                                analyzeFixture sourceCode tree Python.pythonLanguageAdapter "latest_pricing.py"

                            // decision: the includeTestFiles flag re-enables findings in test-named files,
                            // which is how fixtures under a test/ directory get audited.
                            let testFileIncluded =
                                createTestContext
                                    sourceCode
                                    tree
                                    Python.pythonLanguageAdapter
                                    "PricingTest.py"
                                    { defaultThresholds with
                                        MagicNumber =
                                            { Enabled = true
                                              Allowlist = [ 0.0; 1.0; -1.0; 2.0 ]
                                              IncludeTestFiles = true } }
                                |> analyzeMagicNumbers
                                |> _.Violations

                            assertThat (disabled |> List.filter (fun v -> v.Type = Magic) |> List.length) (isEqualTo 0)

                            assertThat
                                (violationsIn customAllowlist numbers
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)

                            assertThat (testFile |> List.filter (fun v -> v.Type = Magic) |> List.length) (isEqualTo 0)
                            assertThat (latestFile |> List.exists (fun v -> v.Type = Magic)) isTrue

                            assertThat
                                (violationsIn testFileIncluded numbers
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 3)
                        }
                    ))
            ) ]
    )
