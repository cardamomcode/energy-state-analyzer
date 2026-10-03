module Energy.Tests.OpaqueBooleanTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Languages
open Energy.Tests.TestUtils

/// Verify the shared test-file policy through the complete pipeline for every supported language.
let private testFilePolicyTests =
    [
        "Python", Python.pythonLanguageAdapter, "python/opaque_boolean.py", "flaggedAssertionBoolean"
        "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/opaqueBoolean.ts", "flaggedAssertionBoolean"
        "F#", FSharp.fSharpLanguageAdapter, "fsharp/OpaqueBoolean.fs", "flaggedAssertionBoolean"
        "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/OpaqueBoolean.kt", "flaggedAssertionBoolean"
        "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/opaque_boolean.cpp", "flaggedAssertionBoolean"
        "C#", CSharp.cSharpLanguageAdapter, "csharp/OpaqueBoolean.cs", "FlaggedAssertionBoolean"
    ]
    |> List.map (fun (label, language, fixture, assertion) ->
        testAsync (
            sprintf "%s: test assertions are exempt by default and included on request" label,
            fun _ ->
                toAsync (
                    task {
                        let! (source, tree) = parseFixture language fixture
                        let range = findFunctionRange source (FunctionName assertion)
                        let extension = fixture.Substring(fixture.LastIndexOf('.'))

                        let hits options fileName =
                            {
                                Source = source
                                Tree = tree
                                Language = language
                                FileName = fileName
                            }
                            |> analyzeWith options
                            |> _.Violations
                            |> fun violations -> violationsIn violations range
                            |> List.filter (fun violation -> violation.Type = OpaqueBoolean)
                            |> List.length

                        for productionName in [ fixture; "latest_pricing"; "contest" ] do
                            assertThat (hits defaultAnalyzeOptions productionName) (isEqualTo 2)

                        let included =
                            { defaultAnalyzeOptions with
                                OpaqueBoolean =
                                    { defaultOpaqueBooleanThresholds with
                                        IncludeTestFiles = true
                                    }
                            }

                        let disabled =
                            { included with
                                OpaqueBoolean =
                                    { included.OpaqueBoolean with
                                        Enabled = false
                                    }
                            }

                        for testName in
                            [
                                "tests/" + fixture
                                "test/" + fixture
                                "tests\\" + fixture
                                "test_pricing" + extension
                                "pricing_test" + extension
                                "PricingTest" + extension
                                "pricing.test" + extension
                            ] do
                            assertThat (hits defaultAnalyzeOptions testName) (isEqualTo 0)
                            assertThat (hits included testName) (isEqualTo 2)
                            assertThat (hits disabled testName) (isEqualTo 0)
                    }
                )
        ))

/// Exercise positional and labeled boolean arguments together with test-file exemptions.
let tests =
    let cases =
        [
            "Python", Python.pythonLanguageAdapter, "python/opaque_boolean.py", "suppressedKeywordArgument"
            "TypeScript",
            TypeScript.typeScriptLanguageAdapter,
            "typescript/opaqueBoolean.ts",
            "suppressedObjectLiteralField"
            "F#", FSharp.fSharpLanguageAdapter, "fsharp/OpaqueBoolean.fs", "suppressedNamedArgument"
            "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/OpaqueBoolean.kt", "suppressedNamedArgument"
            "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/opaque_boolean.cpp", "suppressedLabeledAggregateField"
        ]

    testList (
        "Integration: opaque booleans",
        testFilePolicyTests
        @ (cases
           |> List.map (fun (label, language, fixture, labeled) ->
               testAsync (
                   (sprintf "%s: flags positional values only" label),
                   fun _ ->
                       toAsync (
                           task {
                               let! (source, tree) = parseFixture language fixture
                               let violations = analyzeFixture source tree language fixture
                               let one = findFunctionRange source (FunctionName "flaggedPositionalBoolean")

                               let many =
                                   findFunctionRange source (FunctionName "flaggedPositionalBooleanAmongOthers")

                               let named = findFunctionRange source (FunctionName labeled)
                               let nonCall = findFunctionRange source (FunctionName "suppressedNonCallUsage")

                               let hits range =
                                   violationsIn violations range
                                   |> List.filter (fun v -> v.Type = OpaqueBoolean)
                                   |> List.length

                               assertThat (hits one) (isEqualTo 1)
                               assertThat (hits many) (isEqualTo 1)
                               assertThat (hits named) (isEqualTo 0)
                               assertThat (hits nonCall) (isEqualTo 0)
                           }
                       )
               )))
    )
