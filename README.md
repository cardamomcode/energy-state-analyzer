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

## Cyclomatic Complexity

Counts the number of independent paths through a function. Starting from a base of **1**, every decision point adds **+1**, regardless of how deeply it's nested:

- `if` / `elif` / `while` / `for` / `except`
- `and` / `or`
- ternary (`a if cond else b`)

Two functions with the same number of `if`s score the same, whether those `if`s are sequential or nested five deep — it measures *how many paths exist*, not how hard the code is to follow.

## Cognitive Complexity

Modeled on [SonarSource's metric](https://www.sonarsource.com/resources/cognitive-complexity/): it measures how hard a function is to *read*, so nesting is penalized and straight-line control flow isn't.

- Each decision point (`if`, `elif`, `for`, `while`, `except`, ternary, nested `def`/`lambda`) adds **1 + current nesting depth**.
- `else` adds a flat **+1** — no nesting penalty, since it doesn't add a new branch to reason about.
- Nesting depth only increases when descending into a block body, so an `if` inside two other `if`s scores higher than three sequential `if`s at the top level, even though both have the same cyclomatic complexity.
- Chained boolean operators of the same kind (`a and b and c`) count as a **single** increment rather than one per operator; mixing `and`/`or` starts a new increment.

This project's implementation is a simplified first pass on the SonarSource spec: `for`/`while` `else` clauses are scored like `if`/`else`, boolean-chain merging only looks at the immediate parent operator, and recursive calls aren't specially detected.

## Command-Line Usage

The same detectors also run headlessly, without VS Code — useful for CI or for an AI coding agent that wants to check the complexity of code it just generated and keep refactoring until it's clean:

```bash
npm run compile
node dist/cli.js path/to/file.py
```

It prints violations as JSON and exits `1` if any medium/high-severity violation was found (`0` otherwise), so it can gate a loop:

```bash
node dist/cli.js path/to/file.py \
  --medium-cyclomatic 8 --high-cyclomatic 12 \
  --medium-cognitive 12 --high-cognitive 20
```

## Requirements

The extension activates automatically when you open a `.py` file; it bundles its own Python grammar for parsing (via `web-tree-sitter`), so no external tools are required.

## Extension Settings

Detector thresholds are configurable under **Settings → Energy State Analyzer**:

- `energyStateAnalyzer.cyclomaticComplexity.mediumThreshold` / `.highThreshold`
- `energyStateAnalyzer.cognitiveComplexity.mediumThreshold` / `.highThreshold`

Changes take effect immediately on the active editor.

## Commands

- **Energy State Analyzer: Analyze Energy State** (`energy-state-analyzer.analyze`) — manually re-run analysis on the active editor.

## Architecture

Detector logic lives in `src/core/` and is language-agnostic — each detector takes a parsed tree-sitter tree plus a `LanguageAdapter` describing that grammar's node type names, instead of hardcoding Python's. `src/languages/python.ts` is the one adapter that exists today. `src/extension.ts` (VS Code glue) and `src/cli.ts` (headless entry point) both call into the same `analyzeSource` core function, so adding a new language means writing a new adapter, not duplicating detectors.

## Known Issues

- Nesting depth, file coherence, magic value, and parameter count thresholds are not yet configurable — only cyclomatic and cognitive complexity are.
- Analysis only covers Python; other languages aren't wired up yet, though the core detectors are language-agnostic (see Architecture).
