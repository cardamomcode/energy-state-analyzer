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
              "JSON report retains actionable finding locations and messages",
              fun _ ->
                  let finding =
                      { violation Medium PrimitiveObsession "Introduce a value object so swaps fail type checking." with
                          Line = 5
                          Column = 12
                          Hotspots = [ { Line = 5; Weight = 3 } ] }

                  let json =
                      [ { FilePath = "agents.fs"
                          Violations = [ finding ] } ]
                      |> Energy.CliModes.summaryJson
                      |> Energy.CliNode.stringify

                  assertThat (json.Contains("\"violations\"")) isTrue
                  assertThat (json.Contains("\"line\": 5")) isTrue
                  assertThat (json.Contains("\"column\": 12")) isTrue

                  assertThat
                      (json.Contains("\"message\": \"Introduce a value object so swaps fail type checking.\""))
                      isTrue

                  assertThat (json.Contains("\"hotspots\"")) isTrue
                  assertThat (json.Contains("\"weight\": 3")) isTrue
          )
          test (
              "rule IDs are stable and unique across every violation type",
              fun _ ->
                  let ids =
                      [ Nesting
                        Complexity
                        Cognitive
                        Naming
                        Coherence
                        Magic
                        Parameters
                        Inversion
                        PrimitiveObsession
                        MatchOpportunity
                        LogicalControlFlow
                        OpaqueBoolean
                        ErrorShadowing
                        Suppression ]
                      |> List.map violationRuleId

                  assertThat
                      ids
                      (isEqualTo
                          [ "ESA-001"
                            "ESA-002"
                            "ESA-003"
                            "ESA-004"
                            "ESA-005"
                            "ESA-006"
                            "ESA-007"
                            "ESA-008"
                            "ESA-009"
                            "ESA-010"
                            "ESA-011"
                            "ESA-012"
                            "ESA-013"
                            "ESA-014" ])

                  assertThat (ids |> Set.ofList |> Set.count) (isEqualTo ids.Length)
          )
          test (
              "rule help URIs point to their detector documentation",
              fun _ ->
                  let documentation =
                      [ Nesting, "excessive-nesting.md"
                        Complexity, "cyclomatic-complexity.md"
                        Cognitive, "cognitive-complexity.md"
                        Naming, "file-coherence.md"
                        Coherence, "file-coherence.md"
                        Magic, "magic-values.md"
                        Parameters, "parameter-explosion.md"
                        Inversion, "inversion-opportunities.md"
                        PrimitiveObsession, "primitive-obsession.md"
                        MatchOpportunity, "match-opportunities.md"
                        LogicalControlFlow, "logical-operator-control-flow.md"
                        OpaqueBoolean, "opaque-boolean-literal.md"
                        ErrorShadowing, "error-shadowing.md"
                        Suppression, "suppression.md" ]

                  documentation
                  |> List.iter (fun (violationType, document) ->
                      assertThat
                          ((violationHelpUri violationType).EndsWith(document, System.StringComparison.Ordinal))
                          isTrue)
          )
          test (
              "SARIF report exposes standard rules, one-based locations, and remediation messages",
              fun _ ->
                  let finding =
                      { violation High Magic "Extract this literal to a named constant." with
                          Line = 5
                          Column = 12 }

                  let sarif =
                      [ { FilePath = "agents.fs"
                          Violations = [ finding ] } ]
                      |> Energy.Core.ReportSarif.renderSarif
                      |> Energy.CliNode.stringify

                  assertThat (sarif.Contains("\"version\": \"2.1.0\"")) isTrue
                  assertThat (sarif.Contains("\"ruleId\": \"ESA-006\"")) isTrue
                  assertThat (sarif.Contains("\"id\": \"ESA-006\"")) isTrue

                  assertThat
                      (sarif.Contains(
                          "\"helpUri\": \"https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/magic-values.md\""
                      ))
                      isTrue

                  assertThat (sarif.Contains("\"startLine\": 6")) isTrue
                  assertThat (sarif.Contains("\"startColumn\": 13")) isTrue
                  assertThat (sarif.Contains("Extract this literal to a named constant.")) isTrue
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

                  assertThat
                      (report.IndexOf("## severe.py", System.StringComparison.Ordinal) < report.IndexOf(
                          "## mild.py",
                          System.StringComparison.Ordinal
                      ))
                      isTrue

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
                          // Fully qualified — this file sits at the coherence detector's 10-import
                          // threshold, and Paths is needed at exactly this one call site.
                          let! result = analyzeFile (Energy.Core.Paths.Path "unsupported.txt") "" defaultThresholds

                          assertThat result (isEqualTo (Error(UnsupportedLanguage "unsupported.txt")))
                      }
                  )
          ) ]
    )
