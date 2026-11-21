// # Report
//
// Raw per-file findings become additive severity summaries for JSON, Markdown, and diffs. Scores use fixed
// weights 1/4/9; clean files remain represented in scan counts. ReportHuman.fs owns a separate zero-to-ten
// prioritization view, while ReportDiff.fs compares these additive scores across revisions.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.Report

open Energy.Core.Violation

/// Scoring weights for severity counts, kept as named constants at the top of this module.
///
/// decision: scoring weights are the published report/diff continuity metric (1/4/9); only the
/// low weight is 1, so the medium and high weights become named constants at the top of the module
/// rather than in Core.Config, keeping them visible next to the module's other declarations.
let private mediumWeight = 4
/// Weight of each high-severity finding in the published additive report score.
let private highWeight = 9

/// Counts of findings in each severity band, independent of detector category.
type SeverityCounts = { Low: int; Medium: int; High: int }

/// One analyzed file's report path and detached findings; an empty list records a successfully scanned
/// clean file.
type FileResult =
    {
        FilePath: string
        Violations: EnergyViolation list
    }

/// A file's additive score, severity counts, and counts keyed by stable violation-type names.
type FileSummary =
    {
        FilePath: string
        Score: int
        Counts: SeverityCounts
        ByType: Map<string, int>
    }

/// Ordered per-file summaries plus summed scores and severity counts for the complete scan.
type AggregateSummary =
    {
        Files: FileSummary list
        TotalScore: int
        TotalCounts: SeverityCounts
    }

/// Zero findings in every severity band, used to seed report folds.
let emptyCounts = { Low = 0; Medium = 0; High = 0 }

/// Increment only the severity band of one finding, retaining all other counts.
let private addViolation counts violation =
    match violation.Severity with
    | Low -> { counts with Low = counts.Low + 1 }
    | Medium ->
        { counts with
            Medium = counts.Medium + 1
        }
    | High -> { counts with High = counts.High + 1 }

/// Calculate the additive report metric: low plus four times medium plus nine times high counts.
let private score counts =
    counts.Low + mediumWeight * counts.Medium + highWeight * counts.High

/// Aggregate one file's violations into a weighted score, severity counts, and per-type breakdown.
///
/// invariant: weights 1/4/9 are the published report/diff continuity metric; only structured
/// severity contributes, never numbers embedded in detector messages.
let summarizeFile result =
    let counts = result.Violations |> List.fold addViolation emptyCounts

    let byType =
        result.Violations
        |> List.fold
            (fun types violation ->
                let name = violationTypeName violation.Type
                Map.change name (Option.defaultValue 0 >> (+) 1 >> Some) types)
            Map.empty

    {
        FilePath = result.FilePath
        Score = score counts
        Counts = counts
        ByType = byType
    }

/// Summarize each file in input order and sum its counts and score; empty input yields empty files and zero
/// totals.
let summarize results =
    let files = results |> List.map summarizeFile

    let totalCounts =
        files
        |> List.fold
            (fun counts file ->
                {
                    Low = counts.Low + file.Counts.Low
                    Medium = counts.Medium + file.Counts.Medium
                    High = counts.High + file.Counts.High
                })
            emptyCounts

    {
        Files = files
        TotalScore = files |> List.sumBy _.Score
        TotalCounts = totalCounts
    }

/// Report whether any medium or high finding should make scan mode exit unsuccessfully; low findings alone
/// do not block.
let hasBlockingViolations counts = counts.Medium > 0 || counts.High > 0

/// Render finding counts and weighted scores as a Markdown report.
let renderMarkdownReport summary =
    let noFindingsCount =
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

    [
        "# Energy State Report"
        ""
        sprintf
            "**%d file%s scanned** — %d with no findings, %d with findings"
            fileCount
            suffix
            noFindingsCount
            (fileCount - noFindingsCount)
        ""
        "| File | Score | High | Medium | Low |"
        "| --- | --- | --- | --- | --- |"
    ]
    @ rows
    @ [
        ""
        sprintf
            "**Total score: %d** (%d high, %d medium, %d low)"
            summary.TotalScore
            summary.TotalCounts.High
            summary.TotalCounts.Medium
            summary.TotalCounts.Low
    ]
    |> String.concat "\n"
