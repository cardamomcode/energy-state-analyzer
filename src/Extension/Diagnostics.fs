module Energy.Extension.Diagnostics

open Energy.Core.Violation
open Energy.Extension.DiagnosticModel
open Energy.Extension.Vscode.DiagnosticValues
open Energy.Extension.Vscode.Diagnostics
open Energy.Extension.Vscode.Document
open Energy.Extension.Vscode.Presentation

let private vscodeSeverity =
    function
    | ProblemSeverity.Error -> error
    | ProblemSeverity.Warning -> warning
    | ProblemSeverity.Information -> information

let private vscodeTag =
    function
    | Unnecessary -> unnecessary
    | Deprecated -> deprecated

let private makeVscodeDiagnostic (spec: DiagnosticSpec) =
    let range =
        makeRange
            (Line spec.Range.Line)
            (Column spec.Range.StartColumn)
            (Line spec.Range.Line)
            (Column spec.Range.EndColumn)

    let diagnostic = makeDiagnostic range spec.Message (vscodeSeverity spec.Severity)
    setDiagnosticSource diagnostic "Energy State Analyzer"
    setDiagnosticCode diagnostic spec.Code

    if not spec.Tags.IsEmpty then
        spec.Tags |> List.map vscodeTag |> List.toArray |> setDiagnosticTags diagnostic

    diagnostic

let updateProblemsPanel (collection: obj) (document: obj) (violations: EnergyViolation list) =
    diagnosticSpecs violations
    |> List.map makeVscodeDiagnostic
    |> List.toArray
    |> setDiagnostics collection (documentUri document)
