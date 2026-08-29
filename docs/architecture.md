# Architecture — Energy State Analyzer

The extension is split **by domain** (see triage item #30) so each file stays small enough for the
project's coherence rules to hold and stay easy to follow. This doc is the module map; the per-file
domain header in each source file restates its responsibility at the point of use.

## Coherence rules

Every file must keep **≤12 functions** and **≤10 import sources** (enforced by the `analyzeFileCoherence`
detector, run via `just analyze`). When a module approaches those limits, split it by concern rather than
letting it grow — this is exactly what #30 did to the old single-file `src/extension.ts`.

## Module map

| File | Domain | Owns | Holds state? |
|------|--------|------|--------------|
| `src/extension.ts` | Composition root | `activate`/`deactivate`, event wiring, cross-module coordination | Yes — creates collaborators and threads shared state into them |
| `src/decorations.ts` | Editor decoration presentation | Creating/disposing decoration types (`createDecorations`), per-violation highlight ranges + complexity heatmap (`applyDecorations`) | No — receives a `DecorationSet` from the composition root |
| `src/diagnostics.ts` | Problems-panel presentation | Mapping violations to `vscode.Diagnostic` objects (`updateProblemsPanel`) | No — receives its `DiagnosticCollection` from the composition root |
| `src/grammar.ts` | Tree-sitter grammar lifecycle | `Parser.init` (`initializeParser`), per-language load + cache (`getOrLoadLanguage`) | No — caches live in the composition root, injected via `GrammarContext` |
| `src/analysis.ts` | Document analysis + ignore gating | Turning a parsed buffer into violations (`analyzeDocument`), `.esaignore` / `includeFixtures` gating (`isDocumentIgnored`) | No — pure orchestration over `core/*` |

### The composition root

`src/extension.ts` owns activation wiring and coordination only. It creates collaborators, threads shared
state (grammar caches, the decoration set, the diagnostics collection) into them, and wires event handlers.
It contains **no** detector, presentation, or analysis logic of its own — if you find yourself adding
business logic here, extract it into a domain module instead.

### Analysis pipeline

The actual pipeline entry point is `src/core/analyze.ts`. It parses the active document's text into a
tree-sitter AST and runs independent detector passes over it, each returning `EnergyViolation[]`:

- `analyzeNesting` — flags nesting deeper than 3 levels.
- `analyzeFunctionComplexity` — cyclomatic complexity per function, flags >10.
- `analyzeFileCoherence` — files with too many functions or imports.
- `analyzeMagicValues` — magic numeric/string literals outside constant context.
- `analyzeParameterCount` — functions with >5 parameters.
- `analyzeInversionOpportunities` — dominant if-blocks / nested validation chains rewriteable as guard clauses.
- `extractTypeInformation` — collects type info (currently only logged; future-feature scaffolding).

Each detector does its own `traverse(node)` walk — there's no shared visitor abstraction yet. A final
`applySuppressions` pass (`src/core/suppressions.ts`) filters violations covered by an `esa-ignore` /
`esa-ignore-file` comment and emits low-severity `suppression` findings for unused or unknown directives
(see `docs/detectors/suppression.md`).

### Presentation

Violations are surfaced two ways, both fed from the same `EnergyViolation[]`:

- **Editor decorations** (`src/decorations.ts`) — color/severity ranges (red=high, yellow=medium, green=low)
  rendered as a background tint plus a gutter lightning-bolt icon (`createLightningIcon`), plus the
  complexity heatmap over the lines that drive a flagged function's complexity.
- **Problems panel** (`src/diagnostics.ts`) — the same violations mirrored into a `vscode.DiagnosticCollection`,
  grouped per line with combined messages, severity, and tags.

### Violation model

`EnergyViolation { line, column, type, severity, message }`. The `type` and `severity` string-literal unions
are backed by the `VIOLATION_TYPE` / `SEVERITY` constant objects in `src/types.ts` — keep those two in sync
when adding a new detector.

## Domain boundaries (don't cross them casually)

- **Presentation lives in `decorations.ts` / `diagnostics.ts`.** Never construct a `DecorationType` or
  `Diagnostic` outside those files.
- **All `web-tree-sitter` access is behind `grammar.ts`.** Don't call the parser from an analysis module.
- **Analysis + ignore logic live in `analysis.ts`; detectors stay under `core/`.**

## Adding a new detector

Write an `analyze<Thing>(tree, document): EnergyViolation[]` function that walks `tree.rootNode`, push it
into the list in `src/core/analyze.ts`, and add a `VIOLATION_TYPE` entry if it's a new category. If the
violation needs special range highlighting, add a case in `applyDecorations` (`src/decorations.ts`).

## Build / packaging

Webpack bundles `src/extension.ts` (the composition root) → `dist/extension.js` (CommonJS; `vscode` is treated
as external). `web-tree-sitter`'s own `tree-sitter.wasm` is copied into `dist/` via `CopyWebpackPlugin`; the
per-language grammar WASMs in `grammars/` ship separately and are loaded at runtime by path, not bundled.
