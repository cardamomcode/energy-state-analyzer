module Energy.Extension.DiagnosticModel

open Energy.Core.Violation

// Pure Problems-panel mapping. VS Code constructors live in Diagnostics.fs, leaving these public
// semantics directly testable in the normal Fable/Node suite.

[<RequireQualifiedAccess>]
type ProblemSeverity =
    | Error
    | Warning
    | Information

type ProblemTag =
    | Unnecessary
    | Deprecated

type ProblemRange =
    { Line: int
      StartColumn: int
      EndColumn: int }

type DiagnosticSpec =
    { Range: ProblemRange
      Message: string
      Severity: ProblemSeverity
      Code: string
      Tags: ProblemTag list }

let private diagnosticRangeWidth = 10

let severityFor =
    function
    | High -> ProblemSeverity.Error
    | Medium -> ProblemSeverity.Warning
    | Low -> ProblemSeverity.Information

let private severityRank severity =
    match severityFor severity with
    | ProblemSeverity.Error -> 0
    | ProblemSeverity.Warning -> 1
    | ProblemSeverity.Information -> 2

let tagsFor =
    function
    | Nesting -> [ Unnecessary ]
    | Complexity
    | Cognitive -> [ Deprecated ]
    | _ -> []

// decision: combines same-line findings because VS Code shows one inline problem message per
// line; grouping keeps a lower-priority finding from disappearing behind the leading one.
let diagnosticSpecs (violations: EnergyViolation list) : DiagnosticSpec list =
    violations
    |> List.groupBy _.Line
    |> List.map (fun (_, group) ->
        let ordered =
            group
            |> List.sortBy (fun violation -> severityRank violation.Severity, violation.Column)

        let lead = List.head ordered

        { Range =
            { Line = lead.Line
              StartColumn = lead.Column
              EndColumn = lead.Column + diagnosticRangeWidth }
          Message = ordered |> List.map _.Message |> String.concat " | "
          Severity = severityFor lead.Severity
          Code =
            ordered
            |> List.map (fun violation -> "energy-" + violationTypeName violation.Type)
            |> String.concat ","
          Tags = ordered |> List.collect (fun violation -> tagsFor violation.Type) })
