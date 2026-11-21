// # Diagnostics
//
// This presentation boundary converts pure diagnostic specifications into VS Code objects with source,
// rule codes, severity, and tags. Updating a document replaces its collection entry completely, including
// clearing it for an empty finding list. The extension composition root owns collection lifetime.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Extension.Diagnostics

open Energy.Core.Violation
open Energy.Extension.DiagnosticModel
open Energy.Extension.Vscode.DiagnosticValues
open Energy.Extension.Vscode.Diagnostics
open Energy.Extension.Vscode.Document
open Energy.Extension.Vscode.Presentation

/// Translate the pure diagnostic model's severity to VS Code's runtime enumeration.
let private vscodeSeverity =
    function
    | ProblemSeverity.Error -> error
    | ProblemSeverity.Warning -> warning
    | ProblemSeverity.Information -> information

/// Translate an unnecessary or deprecated model tag to VS Code's runtime enumeration.
let private vscodeTag =
    function
    | Unnecessary -> unnecessary
    | Deprecated -> deprecated

/// Allocate a host diagnostic from its pure specification, assigning its range, source, stable code, and
/// any tags.
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

/// Replace the document URI's diagnostics with the sorted specifications derived from current findings. An
/// empty list clears the displayed findings for that document.
let updateProblemsPanel (collection: obj) (document: obj) (violations: EnergyViolation list) =
    diagnosticSpecs violations
    |> List.map makeVscodeDiagnostic
    |> List.toArray
    |> setDiagnostics collection (documentUri document)
