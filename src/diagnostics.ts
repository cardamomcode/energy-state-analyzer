// Domain: Problems-panel presentation. Owns translating violations into vscode.Diagnostic objects
// (updateProblemsPanel) — severity mapping, per-line grouping, tags, and combined messages. Pure
// transformation with no editor state; the composition root supplies its DiagnosticCollection.
// Coherence: keep ≤12 functions, ≤10 imports.
import * as vscode from 'vscode';
import { EnergyViolation, SEVERITY, VIOLATION_TYPE } from './types';

function toDiagnosticSeverity(severity: string): vscode.DiagnosticSeverity {
    switch (severity) {
        case SEVERITY.HIGH:
            return vscode.DiagnosticSeverity.Error;
        case SEVERITY.MEDIUM:
            return vscode.DiagnosticSeverity.Warning;
        default:
            return vscode.DiagnosticSeverity.Information;
    }
}

function tagsForViolationType(type: string): vscode.DiagnosticTag[] {
    switch (type) {
        case VIOLATION_TYPE.NESTING:
            // decision: reuses DiagnosticTag.Unnecessary (fade/gray-out) for nesting violations — the closest built-in cue for "this structure could be flattened away"
            return [vscode.DiagnosticTag.Unnecessary];
        case VIOLATION_TYPE.COMPLEXITY:
        case VIOLATION_TYPE.COGNITIVE:
            // decision: reuses DiagnosticTag.Deprecated (strikethrough) as a visual cue for complexity violations — VS Code has no "high effort" tag, and Deprecated's strikethrough is the closest built-in signal for "this needs rework"
            return [vscode.DiagnosticTag.Deprecated];
        default:
            return [];
    }
}

// Fixed width for every Problems-panel diagnostic range — not user-configurable; see the
// decision note at its use site in buildLineDiagnostic below.
const DIAGNOSTIC_RANGE_WIDTH = 10;

function groupViolationsByLine(violations: EnergyViolation[]): Map<number, EnergyViolation[]> {
    const byLine = new Map<number, EnergyViolation[]>();
    for (const violation of violations) {
        const group = byLine.get(violation.line);
        if (group) {
            group.push(violation);
        } else {
            byLine.set(violation.line, [violation]);
        }
    }
    return byLine;
}

function buildLineDiagnostic(group: EnergyViolation[]): vscode.Diagnostic {
    // Sort so the highest-severity, then earliest-column violation leads the combined message
    const bySeverityThenColumn = [...group].sort(
        (a, b) => toDiagnosticSeverity(a.severity) - toDiagnosticSeverity(b.severity) || a.column - b.column
    );
    const lead = bySeverityThenColumn[0];

    // decision: uses a fixed-width range for every diagnostic regardless of violation type — the Problems panel only needs a clickable location, unlike applyDecorations' editor highlight which must visually match the flagged construct
    const range = new vscode.Range(lead.line, lead.column, lead.line, lead.column + DIAGNOSTIC_RANGE_WIDTH);

    const message =
        bySeverityThenColumn.length === 1 ? lead.message : bySeverityThenColumn.map((v) => v.message).join(' | ');

    const diagnostic = new vscode.Diagnostic(range, message, toDiagnosticSeverity(lead.severity));
    diagnostic.source = 'Energy State Analyzer';
    diagnostic.code = bySeverityThenColumn.map((v) => `energy-${v.type}`).join(',');

    const tags = bySeverityThenColumn.flatMap((v) => tagsForViolationType(v.type));
    if (tags.length > 0) {
        diagnostic.tags = tags;
    }

    return diagnostic;
}

// decision: groups violations by line before building diagnostics, rather than emitting one
// Diagnostic per violation — VS Code's inline "after-line" problem text shows only a single
// diagnostic's message per line (picked by its own severity/position heuristic), silently
// dropping the rest even though the hover popup correctly lists every diagnostic on that line.
// Merging same-line violations into one Diagnostic with a combined message means the inline
// text can no longer hide a violation the hover would otherwise reveal.
export function updateProblemsPanel(
    diagnosticsCollection: vscode.DiagnosticCollection,
    document: vscode.TextDocument,
    violations: EnergyViolation[]
): void {
    const byLine = groupViolationsByLine(violations);
    const diagnostics = [...byLine.values()].map(buildLineDiagnostic);
    diagnosticsCollection.set(document.uri, diagnostics);
}
