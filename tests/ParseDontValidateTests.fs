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
              "flipped branches return a truthy literal from the guard and stay unextracted",
              fun _ ->
                  toAsync (
                      task {
                          // A truthy literal from the guard branch is not a rejection in any
                          // language, so flipped validators never extract — statement form for the
                          // block grammars, single-expression form for F#/Kotlin.
                          let pythonFlipped =
                              """
def flipped(value: str | None) -> bool:
    if value is not None:
        return True
    return False
"""

                          let typeScriptFlipped =
                              """
function flipped(value: string | null): boolean {
    if (value !== null) { return true; }
    return false;
}
"""

                          let fSharpFlipped =
                              """
module BooleanEdge

let flipped (value: string) = if value <> null then true else false
"""

                          let kotlinFlipped =
                              """
fun flipped(value: String?): Boolean {
    if (value != null) { return true }
    return false
}
"""

                          let cPlusPlusFlipped =
                              """
bool flipped(const char* value) {
    if (value == nullptr) { return true; }
    return false;
}
"""

                          let cSharpFlipped =
                              """
class Flipped {
    bool F(string? value) {
        if (value == null) { return true; }
        return false;
    }
}
"""

                          let cases =
                              [ ("python", Python.pythonLanguageAdapter, pythonFlipped)
                                ("typescript", TypeScript.typeScriptLanguageAdapter, typeScriptFlipped)
                                ("fsharp", FSharp.fSharpLanguageAdapter, fSharpFlipped)
                                ("kotlin", Kotlin.kotlinLanguageAdapter, kotlinFlipped)
                                ("cpp", CPlusPlus.cPlusPlusLanguageAdapter, cPlusPlusFlipped)
                                ("csharp", CSharp.cSharpLanguageAdapter, cSharpFlipped) ]

                          for _, language, source in cases do
                              let! findings = parseDontValidateFindings language "flipped.src" source
                              assertThat findings.Length (isEqualTo 0)
                      }
                  )
          )
          testAsync (
              "both null comparison polarities flag when the rejection is falsy and the success is a literal",
              fun _ ->
                  toAsync (
                      task {
                          let! findings =
                              parseDontValidateFindings
                                  Python.pythonLanguageAdapter
                                  "nullpolarity.py"
                                  """
def positive(value: str | None) -> bool:
    if value is None:
        return False
    return True

def negative(value: str | None) -> bool:
    if value is not None:
        return False
    return True
"""

                          assertThat findings.Length (isEqualTo 2)
                      }
                  )
          )
          testAsync (
              "F# single-expression then-false-else-true flags for an ordinary condition",
              fun _ ->
                  toAsync (
                      task {
                          let! findings =
                              parseDontValidateFindings
                                  FSharp.fSharpLanguageAdapter
                                  "ordinary.fs"
                                  """
module BooleanEdge

let ordinary (pw: string) =
    if pw = "" then false else true
"""

                          assertThat findings.Length (isEqualTo 1)
                      }
                  )
          )
          testAsync (
              "F# single-expression then-false-else-true flags for both null comparison polarities",
              fun _ ->
                  toAsync (
                      task {
                          let! findings =
                              parseDontValidateFindings
                                  FSharp.fSharpLanguageAdapter
                                  "nullpolarity.fs"
                                  """
module BooleanEdge

let positive (value: string) =
    if value = null then false else true

let negative (value: string) =
    if value <> null then false else true
"""

                          assertThat findings.Length (isEqualTo 2)
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
