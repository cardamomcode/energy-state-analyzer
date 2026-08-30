module Energy.Core.Report

open Energy.Core.Violation

type SeverityCounts = { Low: int; Medium: int; High: int }

type FileResult =
    { FilePath: string
      Violations: EnergyViolation list }

type FileSummary =
    { FilePath: string
      Score: int
      Counts: SeverityCounts
      ByType: Map<string, int> }

type AggregateSummary =
    { Files: FileSummary list
      TotalScore: int
      TotalCounts: SeverityCounts }

let emptyCounts = { Low = 0; Medium = 0; High = 0 }

let private addViolation counts violation =
    match violation.Severity with
    | Low -> { counts with Low = counts.Low + 1 }
    | Medium ->
        { counts with
            Medium = counts.Medium + 1 }
    | High -> { counts with High = counts.High + 1 }

let private score counts =
    counts.Low + 4 * counts.Medium + 9 * counts.High

// invariant: weights 1/4/9 are the published report/diff continuity metric; only structured
// severity contributes, never numbers embedded in detector messages.
let summarizeFile result =
    let counts = result.Violations |> List.fold addViolation emptyCounts

    let byType =
        result.Violations
        |> List.fold
            (fun types violation ->
                let name = violationTypeName violation.Type
                Map.change name (Option.defaultValue 0 >> (+) 1 >> Some) types)
            Map.empty

    { FilePath = result.FilePath
      Score = score counts
      Counts = counts
      ByType = byType }

let summarize results =
    let files = results |> List.map summarizeFile

    let totalCounts =
        files
        |> List.fold
            (fun counts file ->
                { Low = counts.Low + file.Counts.Low
                  Medium = counts.Medium + file.Counts.Medium
                  High = counts.High + file.Counts.High })
            emptyCounts

    { Files = files
      TotalScore = files |> List.sumBy _.Score
      TotalCounts = totalCounts }

let hasBlockingViolations counts = counts.Medium > 0 || counts.High > 0

let renderMarkdownReport summary =
    let cleanCount =
        summary.Files |> List.filter (fun file -> file.Score = 0) |> List.length

    let fileCount = summary.Files.Length
    let suffix = if fileCount = 1 then "" else "s"

    let rows =
        summary.Files
        |> List.map (fun file ->
            sprintf
                "| %s | %d | %d | %d | %d |"
                file.FilePath
                file.Score
                file.Counts.High
                file.Counts.Medium
                file.Counts.Low)

    [ "# Energy State Report"
      ""
      sprintf
          "**%d file%s scanned** — %d clean, %d with violations"
          fileCount
          suffix
          cleanCount
          (fileCount - cleanCount)
      ""
      "| File | Score | High | Medium | Low |"
      "| --- | --- | --- | --- | --- |" ]
    @ rows
    @ [ ""
        sprintf
            "**Total score: %d** (%d high, %d medium, %d low)"
            summary.TotalScore
            summary.TotalCounts.High
            summary.TotalCounts.Medium
            summary.TotalCounts.Low ]
    |> String.concat "\n"
