module Energy.CliModes

open System.Threading.Tasks

open Fable.Core.JsInterop

open Energy.CliNode
open Energy.CliRuntime
open Energy.Core.Analyze
open Energy.Core.Esaignore
open Energy.Core.Paths
open Energy.Core.Report
open Energy.Core.ReportDiff
open Energy.Core.ReportHuman
open Energy.Core.Scan
open Energy.Core.Violation
open Energy.Languages.Registry

type ReportFormat = string

let printUsage () =
    error "Usage: energy-state-cli <file.py|.fs|.fsx|.ts> [thresholds...]"

    error
        "       energy-state-cli <path...> [--report json|md|human] [thresholds...]              (scan a directory/subtree)"

    error
        "       energy-state-cli --base-ref <ref> [<path...>] [--report json|md] [thresholds...]  (diff PR head against a base ref)"

    error
        "Thresholds: --medium-nesting N --high-nesting N --medium-cyclomatic N --high-cyclomatic N --medium-cognitive N --high-cognitive N"

    error "Flags: --include-test-files (also flag magic numbers and magic strings in test files)"

let private violationJson violation =
    let hotspots =
        violation.Hotspots
        |> List.map (fun hotspot -> createObj [ "line" ==> hotspot.Line; "weight" ==> hotspot.Weight ])
        |> List.toArray

    createObj
        [ "line" ==> violation.Line
          "column" ==> violation.Column
          "type" ==> violationTypeName violation.Type
          "severity" ==> severityName violation.Severity
          "message" ==> violation.Message
          "hotspots" ==> hotspots ]

let private summaryJson summary =
    let files =
        summary.Files
        |> List.map (fun file ->
            createObj
                [ "filePath" ==> file.FilePath
                  "score" ==> file.Score
                  "counts"
                  ==> createObj
                          [ "low" ==> file.Counts.Low
                            "medium" ==> file.Counts.Medium
                            "high" ==> file.Counts.High ]
                  "byType"
                  ==> (file.ByType
                       |> Map.toList
                       |> List.map (fun (key, value) -> key ==> value)
                       |> createObj) ])
        |> List.toArray

    createObj
        [ "files" ==> files
          "totalScore" ==> summary.TotalScore
          "totalCounts"
          ==> createObj
                  [ "low" ==> summary.TotalCounts.Low
                    "medium" ==> summary.TotalCounts.Medium
                    "high" ==> summary.TotalCounts.High ] ]

let private diffJson entries =
    entries
    |> List.map (fun entry ->
        let status =
            match entry.Status with
            | New -> "new"
            | Improved -> "improved"
            | Worsened -> "worsened"
            | Unchanged -> "unchanged"

        createObj
            [ "filePath" ==> entry.FilePath
              "baseScore" ==> entry.BaseScore
              "headScore" ==> entry.HeadScore
              "delta" ==> entry.Delta
              "status" ==> status ])
    |> List.toArray
    |> box

let runLegacySingleFile (filePath: string) (thresholds: AnalyzeThresholds) : Task<unit> =
    task {
        if resolveLanguageForFile filePath |> Option.isNone then
            error ("Unsupported file type: " + filePath)
            printUsage ()
            exit 2
        else
            let! analysis = analyzePath (Path filePath) thresholds

            match analysis with
            | Error analysisError ->
                error ("energy-state-cli failed: " + analysisErrorMessage analysisError)
                exit 1
            | Ok violations ->
                output (stringify (violations |> List.map violationJson |> List.toArray |> box))

                exit (
                    if
                        violations
                        |> List.exists (fun violation -> violation.Severity = Medium || violation.Severity = High)
                    then
                        1
                    else
                        0
                )
    }

let runScan (paths: string list) (thresholds: AnalyzeThresholds) (reportFormat: ReportFormat) : Task<unit> =
    task {
        let! analysis = analyzeFiles (resolveSupportedFiles paths (cwd ())) thresholds

        match analysis with
        | Error analysisError ->
            error ("energy-state-cli failed: " + analysisErrorMessage analysisError)
            exit 1
        | Ok results ->
            let summary = summarize results

            output (
                match reportFormat with
                | "human" -> renderHumanReport results
                | "md" -> renderMarkdownReport summary
                | _ -> stringify (summaryJson summary)
            )

            exit (if hasBlockingViolations summary.TotalCounts then 1 else 0)
    }

let private changedFilesFromGit baseRef =
    execFileSync
        "git"
        [| "diff"; "--name-only"; "--diff-filter=d"; baseRef + "...HEAD" |]
        (createObj [ "encoding" ==> "utf8" ])
    |> fun result ->
        result.Split('\n')
        |> Array.map _.Trim()
        |> Array.filter ((<>) "")
        |> Array.toList

// decision: a missing base version is a normal newly-added/renamed file, not a failed analysis;
// git's own stderr is intentionally suppressed so one concise explanatory line is emitted.
let private readAtRef reference filePath =
    try
        Some(
            execFileSync
                "git"
                [| "show"; reference + ":" + filePath |]
                (createObj [ "encoding" ==> "utf8"; "stdio" ==> [| "ignore"; "pipe"; "ignore" |] ])
        )
    with _ ->
        error (
            "energy-state-cli: could not read "
            + filePath
            + " at "
            + reference
            + " (new file or rename) — treating as new"
        )

        None

let runDiff
    (baseRef: string)
    (explicitPaths: string list)
    (thresholds: AnalyzeThresholds)
    (reportFormat: ReportFormat)
    : Task<unit> =
    task {
        let rootDir = cwd ()
        let patterns = loadIgnorePatterns rootDir

        let changed =
            (if explicitPaths.IsEmpty then
                 changedFilesFromGit baseRef
             else
                 explicitPaths)
            |> List.filter (fun filePath ->
                resolveLanguageForFile filePath |> Option.isSome && existsSync (Path filePath))
            |> List.filter (fun filePath -> not (isIgnored (resolvePath (Path filePath)) (Path rootDir) patterns))

        let rec analyzeChanged files bases heads =
            task {
                match files with
                | [] -> return Ok(List.rev bases, List.rev heads)
                | filePath :: remaining ->
                    let! headAnalysis = analyzePath (Path filePath) thresholds

                    match headAnalysis with
                    | Error analysisError -> return Error analysisError
                    | Ok headViolations ->
                        let head =
                            summarizeFile
                                { FilePath = filePath
                                  Violations = headViolations }

                        match readAtRef baseRef filePath with
                        | None -> return! analyzeChanged remaining bases (head :: heads)
                        | Some baseSource ->
                            let! baseAnalysis = analyzeFile (Path filePath) baseSource thresholds

                            match baseAnalysis with
                            | Error analysisError -> return Error analysisError
                            | Ok baseViolations ->
                                let baseSummary =
                                    summarizeFile
                                        { FilePath = filePath
                                          Violations = baseViolations }

                                return! analyzeChanged remaining (baseSummary :: bases) (head :: heads)
            }

        let! analysis = analyzeChanged changed [] []

        match analysis with
        | Error analysisError ->
            error ("energy-state-cli failed: " + analysisErrorMessage analysisError)
            exit 1
        | Ok(bases, heads) ->
            let entries = diffSummaries bases heads

            output (
                if reportFormat = "md" then
                    renderDiffMarkdown entries baseRef
                else
                    stringify (diffJson entries)
            )

            // invariant: diff mode blocks only regressions; existing debt and newly added files are
            // reported but do not fail a PR until their score worsens relative to the base revision.
            exit (
                if entries |> List.exists (fun entry -> entry.Status = Worsened) then
                    1
                else
                    0
            )
    }
