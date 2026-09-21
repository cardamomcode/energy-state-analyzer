module Energy.Tests.ParseDontValidateTests

open Scriptorium.Nib.Assertion
open Scriptorium.Quill
open type Scriptorium.Quill.Test
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Core.Violation
open Energy.Languages
open Energy.Tests.TestUtils

/// Exercise rule configuration and line/file suppression against the shared fixture.
let tests =
    testList (
        "Parse, don't validate configuration",
        [
            testAsync (
                "disabled rule produces no findings and line and file suppressions consume the findings",
                fun _ ->
                    toAsync (
                        task {
                            let language = Python.pythonLanguageAdapter
                            let! source, tree = parseFixture language "python/parse_dont_validate.py"

                            let input =
                                {
                                    Source = source
                                    Tree = tree
                                    Language = language
                                    FileName = "validators.py"
                                }

                            let enabled = analyze input

                            let findings =
                                enabled.Violations |> List.filter (fun v -> v.Type = ParseDontValidate)

                            assertThat findings.Length (isEqualTo 12)

                            let options =
                                { defaultAnalyzeOptions with
                                    ParseDontValidate = { Enabled = false }
                                }

                            let disabled = analyzeWith options input

                            assertThat
                                (disabled.Violations |> List.exists (fun v -> v.Type = ParseDontValidate))
                                isFalse

                            let lines = source.Split('\n')
                            let first = findings.Head
                            lines.[first.Line] <- lines.[first.Line] + " # esa-ignore: parse-dont-validate"

                            let suppressed =
                                Energy.Core.Suppressions.applySuppressions findings (String.concat "\n" lines)

                            assertThat suppressed.Violations.Length (isEqualTo 11)
                            assertThat suppressed.SuppressionNotes.Length (isEqualTo 0)

                            // File-level suppression: a reasoned esa-ignore-file directive triages the
                            // whole validation module instead of every guard (the documented pattern).
                            let fileSuppressed =
                                Energy.Core.Suppressions.applySuppressions
                                    findings
                                    (source + "\n# esa-ignore-file: parse-dont-validate")

                            assertThat fileSuppressed.Violations.Length (isEqualTo 0)
                            assertThat fileSuppressed.SuppressionNotes.Length (isEqualTo 0)
                        }
                    )
            )
        ]
    )
