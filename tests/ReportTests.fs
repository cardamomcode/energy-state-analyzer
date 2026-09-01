module Energy.Tests.ReportTests

open System.Threading.Tasks

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.CliRuntime
open Energy.Core.Analyze
open Energy.Core.Report
open Energy.Core.ReportDiff
open Energy.Core.ReportHuman
open Energy.Core.Violation

let private violation severity violationType message =
    { Line = 0
      Column = 0
      Type = violationType
      Severity = severity
      Message = message
      Hotspots = [] }

let private cyclomatic value severity =
    violation
        severity
        Complexity
        (sprintf "High cyclomatic complexity: %d. Consider breaking down this function." value)

let tests =
    testList (
        "Integration: report (summarize/diff/render)",
        [ test (
              "summarizeFile scores by severity weight and tallies counts/types",
              fun _ ->
                  let summary =
                      summarizeFile
                          { FilePath = "a.py"
                            Violations =
                              [ violation Low Complexity "test"
                                violation Medium Complexity "test"
                                violation High Complexity "test"
                                violation High Complexity "test" ] }

                  assertThat summary.Score (isEqualTo 23)
                  assertThat summary.Counts (isEqualTo { Low = 1; Medium = 1; High = 2 })
                  assertThat (Map.find "complexity" summary.ByType) (isEqualTo 4)
          )
          test (
              "summarize aggregates totals across files",
              fun _ ->
                  let summary =
                      summarize
                          [ { FilePath = "a.py"
                              Violations = [ violation High Complexity "test" ] }
                            { FilePath = "b.py"
                              Violations = [ violation Medium Complexity "test"; violation Low Complexity "test" ] } ]

                  assertThat summary.TotalScore (isEqualTo 14)
                  assertThat summary.TotalCounts (isEqualTo { Low = 1; Medium = 1; High = 1 })
          )
          test (
              "markdown report includes per-file rows and totals",
              fun _ ->
                  let markdown =
                      summarize
                          [ { FilePath = "a.py"
                              Violations = [ violation High Complexity "test" ] }
                            { FilePath = "clean.py"
                              Violations = [] } ]
                      |> renderMarkdownReport

                  assertThat (markdown.Contains("| a.py | 9 | 1 | 0 | 0 |")) isTrue
                  assertThat (markdown.Contains("1 clean, 1 with violations")) isTrue
          )
          test (
              "diff identifies every status and renders deltas",
              fun _ ->
                  let file path score =
                      { FilePath = path
                        Score = score
                        Counts = emptyCounts
                        ByType = Map.empty }

                  let entries =
                      diffSummaries
                          [ file "worse.py" 0; file "better.py" 9; file "same.py" 4 ]
                          [ file "worse.py" 9; file "better.py" 0; file "same.py" 4; file "new.py" 1 ]

                  let byPath = entries |> List.map (fun entry -> entry.FilePath, entry) |> Map.ofList
                  assertThat (Map.find "worse.py" byPath).Status (isEqualTo Worsened)
                  assertThat (Map.find "better.py" byPath).Status (isEqualTo Improved)
                  assertThat (Map.find "same.py" byPath).Status (isEqualTo Unchanged)
                  assertThat (Map.find "new.py" byPath).Status (isEqualTo New)
                  let markdown = renderDiffMarkdown entries "origin/main"
                  assertThat (markdown.Contains("| worse.py | 0 | 9 | +9 | 🔴 worsened |")) isTrue
          )
          test (
              "human report preserves complexity scores, fallback scores, and worst-first ordering",
              fun _ ->
                  let report =
                      renderHumanReport
                          [ { FilePath = "mild.py"
                              Violations = [ cyclomatic 15 Medium ] }
                            { FilePath = "severe.py"
                              Violations = [ cyclomatic 60 High ] }
                            { FilePath = "pattern.py"
                              Violations = [ violation High Coherence "test" ] }
                            { FilePath = "clean.py"
                              Violations = [] } ]

                  assertThat (report.Contains("## severe.py — Critical (score 9.1)")) isTrue
                  assertThat (report.Contains("## pattern.py — High (score 7.5)")) isTrue
                  assertThat (report.IndexOf("## severe.py") < report.IndexOf("## mild.py")) isTrue
                  assertThat (report.Contains("**Repo score: 9.1 (Critical)**")) isTrue
          )
          test (
              "complexity scores retain documented boundaries",
              fun _ ->
                  assertThat (complexityToScore 10) (isEqualTo 3.9)
                  assertThat (complexityToScore 20) (isEqualTo 6.9)
                  assertThat (classifyComplexityScore 34) (isEqualTo HighRisk)
                  assertThat (classifyComplexityScore 60) (isEqualTo Critical)
          )
          testAsync (
              "unsupported CLI input becomes a typed analysis error",
              fun _ ->
                  toAsync (
                      task {
                          let! result = analyzeFile "unsupported.txt" "" defaultThresholds

                          assertThat result (isEqualTo (Error(UnsupportedLanguage "unsupported.txt")))
                      }
                  )
          ) ]
    )
