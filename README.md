# Energy State Analyzer

Visualizes "energy states" in Python code as you edit: parts of a file that are complex, deeply nested, or otherwise harder to understand and maintain get highlighted with colored gutter icons, inline decorations, and entries in the Problems panel.

## Features

- **Real-time analysis** of the active Python file, re-run on every edit and on editor focus change.
- **Cyclomatic complexity** — flags functions with too many independent execution paths (`if`/`for`/`while`/`except`/boolean operators/ternaries all count equally, regardless of nesting).
- **Cognitive complexity** — flags functions that are hard to *read*, weighting each decision point by how deeply it's nested and not penalizing early-return guard clauses.
- **Excessive nesting** — flags `if`/`for`/`while`/`with` blocks nested more than 3 levels deep.
- **File coherence** — flags files with too many functions or imports (a sign of "utils/helpers sprawl").
- **Magic values** — flags suspicious numeric/string literals used outside of a constant definition.
- **Parameter explosion** — flags functions with more than 5 parameters.
- **Inversion opportunities** — flags large dominant `if` blocks and nested validation chains that could be rewritten as guard clauses with early returns.

Violations are shown three ways:
- A colored background + gutter lightning-bolt icon on the affected lines (red = high severity, yellow = medium, green = low).
- A hover tooltip explaining the specific violation.
- An entry in the Problems panel, sourced as "Energy State Analyzer".

## Requirements

No configuration needed. The extension activates automatically when you open a `.py` file; it bundles its own Python grammar for parsing (via `web-tree-sitter`), so no external tools are required.

## Extension Settings

This extension does not currently contribute any user-configurable settings — thresholds for each detector (e.g. cyclomatic complexity > 10, cognitive complexity > 15, nesting depth > 3) are fixed in code.

## Commands

- **Energy State Analyzer: Analyze Energy State** (`energy-state-analyzer.analyze`) — manually re-run analysis on the active editor.

## Known Issues

- Detection thresholds are not yet configurable per-project.
- Analysis only covers Python; other languages are not yet supported.

## Release Notes

### 0.0.1

Initial version: cyclomatic complexity, cognitive complexity, nesting depth, file coherence, magic values, parameter count, and inversion-opportunity detectors, with gutter/hover/Problems-panel reporting for Python files.
