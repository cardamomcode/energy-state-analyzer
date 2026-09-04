module Energy.Tests.OpaqueBooleanTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/opaque_boolean.py", "suppressedKeywordArgument"
          "TypeScript",
          TypeScript.typeScriptLanguageAdapter,
          "typescript/opaqueBoolean.ts",
          "suppressedObjectLiteralField"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/OpaqueBoolean.fs", "suppressedNamedArgument"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/OpaqueBoolean.kt", "suppressedNamedArgument"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/opaque_boolean.cpp", "suppressedLabeledAggregateField" ]

    testList (
        "Integration: opaque booleans",
        cases
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
            ))
    )
