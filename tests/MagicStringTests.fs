module Energy.Tests.MagicStringTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Detectors.MagicString
open Energy.Core.Position
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin
open Energy.Tests.TestUtils

// decision: exercises the registered pipeline over shared fixtures, proving the adapter hooks
// recognize decision-point strings consistently in every supported grammar.
let tests =
    let cases =
        [ "Python", PYTHON, "python/magicString.py"
          "TypeScript", TYPESCRIPT, "typescript/magicString.ts"
          "F#", FSHARP, "fsharp/magicString.fs"
          "Kotlin", KOTLIN, "kotlin/magicString.kt" ]

    let fixtureTests =
        cases
        |> List.map (fun (label, language, fixture) ->
            testAsync (
                (sprintf "%s: repeated comparisons produce one grouped finding" label),
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture language fixture
                            let violations = analyzeSource sourceCode tree language fixture
                            assertValidPositions violations sourceCode
                            let clean = findFunctionRange sourceCode "cleanValues"
                            let strings = findFunctionRange sourceCode "flaggedMagicString"

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)

                            let hits = violationsIn violations strings |> List.filter (fun v -> v.Type = Magic)
                            assertThat hits.Length (isEqualTo 1)
                            assertThat (List.head hits).Message (satisfy (fun message -> message.Contains("pending")))
                        }
                    ))
            ))

    testList (
        "Integration: magic strings (real code examples)",
        fixtureTests
        @ [ testAsync (
                "Python membership strings and repeated dictionary keys are grouped",
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture PYTHON "python/magicString.py"
                            let violations = analyzeSource sourceCode tree PYTHON "magicString.py"
                            let membership = findFunctionRange sourceCode "flaggedMembership"
                            let dictKey = findFunctionRange sourceCode "flaggedDictKey"

                            let membershipHits =
                                violationsIn violations membership |> List.filter (fun v -> v.Type = Magic)

                            let keyHits =
                                violationsIn violations dictKey |> List.filter (fun v -> v.Type = Magic)

                            assertThat membershipHits.Length (isEqualTo 1)

                            assertThat
                                (List.head membershipHits).Message
                                (satisfy (fun message -> message.Contains("queued")))

                            assertThat keyHits.Length (isEqualTo 1)
                        }
                    ))
            )
            testAsync (
                "options and interpolated keys retain their exemptions",
                (fun _ ->
                    toAsync (
                        task {
                            let! (sourceCode, tree) = parseFixture PYTHON "python/magicString.py"
                            let positions = createPositionLookup sourceCode
                            let clean = findFunctionRange sourceCode "cleanValues"
                            let strings = findFunctionRange sourceCode "flaggedMagicString"

                            let disabled =
                                analyzeMagicStrings
                                    tree
                                    positions
                                    PYTHON
                                    { Enabled = false
                                      MinDuplicates = 2
                                      Allowlist = [] }

                            let singleUse =
                                analyzeMagicStrings
                                    tree
                                    positions
                                    PYTHON
                                    { Enabled = true
                                      MinDuplicates = 1
                                      Allowlist = [ ""; "utf-8"; "__main__" ] }

                            let customAllowlist =
                                analyzeMagicStrings
                                    tree
                                    positions
                                    PYTHON
                                    { Enabled = true
                                      MinDuplicates = 2
                                      Allowlist = [ ""; "utf-8"; "__main__"; "pending" ] }

                            assertThat (disabled |> List.filter (fun v -> v.Type = Magic) |> List.length) (isEqualTo 0)

                            assertThat
                                (violationsIn singleUse clean
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length > 0)
                                isTrue

                            assertThat
                                (violationsIn customAllowlist strings
                                 |> List.filter (fun v -> v.Type = Magic)
                                 |> List.length)
                                (isEqualTo 0)
                        }
                    ))
            ) ]
    )
