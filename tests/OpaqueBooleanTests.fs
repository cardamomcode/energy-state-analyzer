module Energy.Tests.OpaqueBooleanTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", PYTHON, "python/opaqueBoolean.py", "suppressedKeywordArgument"
          "TypeScript", TYPESCRIPT, "typescript/opaqueBoolean.ts", "suppressedObjectLiteralField"
          "F#", FSHARP, "fsharp/opaqueBoolean.fs", "suppressedNamedArgument"
          "Kotlin", KOTLIN, "kotlin/opaqueBoolean.kt", "suppressedNamedArgument" ]

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
                            let violations = analyzeSource source tree language fixture
                            let one = findFunctionRange source "flaggedPositionalBoolean"
                            let many = findFunctionRange source "flaggedPositionalBooleanAmongOthers"
                            let named = findFunctionRange source labeled
                            let nonCall = findFunctionRange source "suppressedNonCallUsage"

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
