// # ReportDiff
//
// Revision comparison joins head summaries to base scores by exact file path. It retains head order, marks
// missing base paths as new, and excludes deleted files because no head row exists. The CLI blocks only
// Worsened entries; the Markdown renderer preserves new-file absence with dashes.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.ReportDiff

open Energy.Core.Report

/// Score comparison for a head file: New lacks a base row, Improved decreases, Worsened increases, and
/// Unchanged preserves the score.
type DiffStatus =
    | New
    | Improved
    | Worsened
    | Unchanged

/// One head file's score comparison. BaseScore is absent for a new file; Delta is head minus base, or the
/// head score when no base exists.
type DiffEntry =
    {
        FilePath: string
        BaseScore: int option
        HeadScore: int
        Delta: int
        Status: DiffStatus
    }

/// Compare head files against base scores by exact path, preserving head order and omitting deleted files.
/// Missing base paths are New even when their score is zero.
let diffSummaries (baseSummaries: FileSummary list) (headSummaries: FileSummary list) =
    let scores =
        baseSummaries |> List.map (fun file -> file.FilePath, file.Score) |> Map.ofList

    headSummaries
    |> List.map (fun file ->
        let baseScore = Map.tryFind file.FilePath scores

        let delta =
            baseScore
            |> Option.map (fun value -> file.Score - value)
            |> Option.defaultValue file.Score

        let status =
            match baseScore with
            | None -> New
            | Some _ when delta < 0 -> Improved
            | Some _ when delta > 0 -> Worsened
            | Some _ -> Unchanged

        {
            FilePath = file.FilePath
            BaseScore = baseScore
            HeadScore = file.Score
            Delta = delta
            Status = status
        })

/// Choose the visual marker for a diff status in Markdown rows.
let private icon =
    function
    | New -> "🆕"
    | Improved -> "🟢"
    | Worsened -> "🔴"
    | Unchanged -> "⚪"

/// Choose the lowercase status text accompanying a diff marker.
let private name =
    function
    | New -> "new"
    | Improved -> "improved"
    | Worsened -> "worsened"
    | Unchanged -> "unchanged"

/// Render ordered score comparisons and status totals against the named base ref. New rows show dashes for
/// base and delta; empty input still produces a table and zero totals.
let renderDiffMarkdown (entries: DiffEntry list) (baseRef: string) =
    let rows =
        entries
        |> List.map (fun entry ->
            let baseScore =
                entry.BaseScore |> Option.map (string<int>) |> Option.defaultValue "—"

            let delta =
                if entry.BaseScore.IsNone then "—"
                elif entry.Delta > 0 then "+" + string<int> entry.Delta
                else string<int> entry.Delta

            sprintf
                "| %s | %s | %d | %s | %s %s |"
                entry.FilePath
                baseScore
                entry.HeadScore
                delta
                (icon entry.Status)
                (name entry.Status))

    let worsened =
        entries |> List.filter (fun entry -> entry.Status = Worsened) |> List.length

    let improved =
        entries |> List.filter (fun entry -> entry.Status = Improved) |> List.length

    let newFiles =
        entries |> List.filter (fun entry -> entry.Status = New) |> List.length

    let suffix = if entries.Length = 1 then "" else "s"

    [
        sprintf "# Energy State Diff vs `%s`" baseRef
        ""
        "| File | Base | Head | Δ | Status |"
        "| --- | --- | --- | --- | --- |"
    ]
    @ rows
    @ [
        ""
        sprintf
            "_%d file%s changed, %d worsened, %d improved, %d new._"
            entries.Length
            suffix
            worsened
            improved
            newFiles
    ]
    |> String.concat "\n"
