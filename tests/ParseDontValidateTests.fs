module Energy.Tests.ParseDontValidateTests

open Scriptorium.Nib.Assertion
open Scriptorium.Quill
open type Scriptorium.Quill.Test
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Languages
open Energy.Tests.TestUtils

/// Run one inline source through the complete registered pipeline and return the
/// parse-don't-validate findings only.
let private parseDontValidateFindings (language: Energy.Core.LanguageAdapter.LanguageAdapter) fileName source =
    task {
        let! root = parseWith (Path(cwd () + "/" + language.GrammarPath)) source

        let input =
            { Source = source
              Tree = root
              Language = language
              FileName = fileName }

        return
            analyze input
            |> fun result -> result.Violations
            |> List.filter (fun violation -> violation.Type = ParseDontValidate)
    }

/// Exercise rule configuration, suppression, and the boolean-validator edge shapes.
let tests =
    testList (
        "Parse, don't validate configuration",
        [ testAsync (
              "disabled rule produces no findings and line and file suppressions consume the findings",
              fun _ ->
                  toAsync (
                      task {
                          let language = Python.pythonLanguageAdapter
                          let! source, tree = parseFixture language "python/parse_dont_validate.py"

                          let input =
                              { Source = source
                                Tree = tree
                                Language = language
                                FileName = "validators.py" }

                          let enabled = analyze input

                          let findings =
                              enabled.Violations |> List.filter (fun v -> v.Type = ParseDontValidate)

                          assertThat findings.Length (isEqualTo 11)

                          let options =
                              { defaultAnalyzeOptions with
                                  ParseDontValidate = { Enabled = false } }

                          let disabled = analyzeWith options input
                          assertThat (disabled.Violations |> List.exists (fun v -> v.Type = ParseDontValidate)) isFalse

                          let lines = source.Split('\n')
                          let first = findings.Head
                          lines.[first.Line] <- lines.[first.Line] + " # esa-ignore: parse-dont-validate"

                          let suppressed =
                              Energy.Core.Suppressions.applySuppressions findings (String.concat "\n" lines)

                          assertThat suppressed.Violations.Length (isEqualTo 10)
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
          testAsync (
              "the non-None guard polarity flags like the None polarity when the success is a literal",
              fun _ ->
                  toAsync (
                      task {
                          // The None polarity is covered by flaggedNullBooleanValidator in the
                          // fixture matrix; this pins that comparison direction does not matter.
                          let! findings =
                              parseDontValidateFindings
                                  Python.pythonLanguageAdapter
                                  "nullpolarity.py"
                                  """
def negative(value: str | None) -> bool:
    if value is not None:
        return False
    return True
"""

                          assertThat findings.Length (isEqualTo 1)
                      }
                  )
          )
          testAsync (
              "F# single-expression then-false-else-true flags for the non-null comparison polarity",
              fun _ ->
                  toAsync (
                      task {
                          // The null polarity is covered by flaggedNullBooleanValidator in the
                          // fixture matrix; this pins that comparison direction does not matter.
                          let! findings =
                              parseDontValidateFindings
                                  FSharp.fSharpLanguageAdapter
                                  "nullpolarity.fs"
                                  """
module BooleanEdge

let negative (value: string) =
    if value <> null then false else true
"""

                          assertThat findings.Length (isEqualTo 1)
                      }
                  )
          )
          testAsync (
              "F# throwing-then if/else stays unextracted (documented limitation)",
              fun _ ->
                  toAsync (
                      task {
                          let! findings =
                              parseDontValidateFindings
                                  FSharp.fSharpLanguageAdapter
                                  "throwingthen.fs"
                                  """
module BooleanEdge

let throwingThen (value: string) =
    if value = null then failwith "missing" else true
"""

                          assertThat findings.Length (isEqualTo 0)
                      }
                  )
          )
          testAsync (
              "Kotlin expression-form if with a falsy then branch flags like F#'s",
              fun _ ->
                  toAsync (
                      task {
                          let! findings =
                              parseDontValidateFindings
                                  Kotlin.kotlinLanguageAdapter
                                  "expression.kt"
                                  """
fun ordinary(pw: String): Boolean {
    if ('@' !in pw) false else true
}
"""

                          assertThat findings.Length (isEqualTo 1)
                      }
                  )
          ) ]
    )
