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

For functions flagged as too complex (cyclomatic or cognitive), a progressive red heatmap is also painted across the function body: each contributing line (an `if`, `for`, `and`, etc.) is shaded from light to dark red based on how much it drives up that function's complexity relative to its own worst line — so you can see exactly which branches to break apart first, instead of just knowing the function as a whole is complex.

## Requirements

The extension activates automatically when you open a `.py` file; it bundles its own Python grammar for parsing (via `web-tree-sitter`), so no external tools are required.

## Extension Settings

Detector thresholds are configurable under **Settings → Energy State Analyzer**:

- `energyStateAnalyzer.cyclomaticComplexity.mediumThreshold` / `.highThreshold`
- `energyStateAnalyzer.cognitiveComplexity.mediumThreshold` / `.highThreshold`

Changes take effect immediately on the active editor.

## Commands

- **Energy State Analyzer: Analyze Energy State** (`energy-state-analyzer.analyze`) — manually re-run analysis on the active editor.

## Known Issues

- Nesting depth, file coherence, magic value, and parameter count thresholds are not yet configurable — only cyclomatic and cognitive complexity are.
- Analysis only covers Python; other languages are not yet supported.
