// # DiagnosticModel
//
// Problems-panel specifications group findings by line, order each group by severity and column, and join
// their messages and stable rule codes. The leading finding supplies severity and range; tags are
// combined. These detached specifications need no VS Code runtime and are converted to host objects by
// Diagnostics.fs.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Extension.DiagnosticModel

open Energy.Core.Violation

/// Pure Problems-panel mapping. VS Code constructors live in Diagnostics.fs, leaving these public
/// semantics directly testable in the normal Fable/Node suite.

[<RequireQualifiedAccess>]
type ProblemSeverity =
    | Error
    | Warning
    | Information

/// Presentation hints mapped to VS Code's unnecessary and deprecated tags; they do not change analyzer
/// severity.
type ProblemTag =
    | Unnecessary
    | Deprecated

/// Zero-based single-line diagnostic range, with an exclusive end column.
type ProblemRange =
    {
        Line: int
        StartColumn: int
        EndColumn: int
    }

/// Host-independent grouped diagnostic with range, joined messages, leading severity, stable rule codes,
/// and presentation tags.
type DiagnosticSpec =
    {
        Range: ProblemRange
        Message: string
        Severity: ProblemSeverity
        Code: string
        Tags: ProblemTag list
    }

/// Fixed character width of the leading finding's diagnostic range; this model has no document text with
/// which to clamp it.
let private diagnosticRangeWidth = 10

/// Map high, medium, and low findings to error, warning, and information presentation levels.
let severityFor =
    function
    | High -> ProblemSeverity.Error
    | Medium -> ProblemSeverity.Warning
    | Low -> ProblemSeverity.Information

/// Order findings by error, warning, then information so each line's strongest finding leads.
let private severityRank severity =
    match severityFor severity with
    | ProblemSeverity.Error -> 0
    | ProblemSeverity.Warning -> 1
    | ProblemSeverity.Information -> 2

/// Assign unnecessary to nesting and deprecated to complexity findings; other categories receive no tags.
let tagsFor =
    function
    | Nesting -> [ Unnecessary ]
    | Complexity
    | Cognitive -> [ Deprecated ]
    | _ -> []

/// Group same-line violations into single Problems-panel entries, ordered by severity.
///
/// decision: combines same-line findings because VS Code shows one inline problem message per
/// line; grouping keeps a lower-priority finding from disappearing behind the leading one.
let diagnosticSpecs (violations: EnergyViolation list) : DiagnosticSpec list =
    violations
    |> List.groupBy _.Line
    |> List.map (fun (_, group) ->
        let ordered =
            group
            |> List.sortBy (fun violation -> severityRank violation.Severity, violation.Column)

        let lead = List.head ordered

        {
            Range =
                {
                    Line = lead.Line
                    StartColumn = lead.Column
                    EndColumn = lead.Column + diagnosticRangeWidth
                }
            Message = ordered |> List.map _.Message |> String.concat " | "
            Severity = severityFor lead.Severity
            Code =
                ordered
                |> List.map (fun violation -> violationRuleId violation.Type)
                |> String.concat ","
            Tags = ordered |> List.collect (fun violation -> tagsFor violation.Type)
        })
