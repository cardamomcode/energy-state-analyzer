# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Project Overview

Energy State Analyzer is a VS Code extension that visualizes "energy states" in Python, F#, TypeScript, and Kotlin code via real-time static analysis. It parses source with `web-tree-sitter` (per-language WASM grammars in `grammars/`) and highlights code that is complex, deeply nested, or otherwise hard to maintain using editor decorations, gutter icons, and Problems-panel diagnostics.

See `energy-state.md` for the original design doc (energy-state principle, planned "detection agents", and known issues/next steps at project inception).

## Build Commands

Commands are wrapped in a `Justfile`; run `just --list` to see all of them. Prefer these over calling `npm run` directly.

```bash
just install       # npm install
just build         # Build extension bundle via webpack (dev mode)
just watch         # Webpack in watch mode
just lint          # ESLint over src/**/*.ts
just format        # Format src/**/*.ts in place with Prettier
just format-check  # Check formatting without writing changes (used by CI)
just analyze       # Run the CLI's own analyzer over src/ (or `just analyze <path...>` for specific files/dirs)
just test          # compile-tests + compile + lint, then run the VS Code extension test host
just pack          # Production build + package into a .vsix via vsce
just clean         # Remove build artifacts (dist, out, *.vsix)
```

The underlying `npm run` scripts (`compile`, `watch`, `package`, `lint`, `format`, `format-check`, `compile-tests`, `watch-tests`, `pretest`, `test`, `analyze`) still work directly if you need finer control than the Justfile recipes give you.

To run and debug the extension interactively, press `F5` in VS Code — this launches an Extension Development Host with the extension loaded, per `.vscode/launch.json`.

There is a single test suite (`src/test/extension.test.ts`); there's no mechanism yet to run a single test by name — use the Extension Test Runner's Testing view in VS Code, or edit the suite temporarily with `.only`.

## Architecture

The extension is split by domain (see #30) so each file stays small enough for the project's coherence rules to hold and stay easy to follow — see `docs/architecture.md` for the full map. There are four domain modules plus a thin composition root:

- **`src/extension.ts`** — composition root. Owns activation wiring (`activate`) and cross-module coordination only: it creates collaborators, threads shared state (grammar caches, decoration set, diagnostics collection) into them, and wires event handlers. It contains no detector, presentation, or analysis logic of its own — if you find yourself adding business logic here, extract it into a domain module instead. `deactivate` disposes the decoration set and diagnostics collection.
- **`src/decorations.ts`** — editor decoration presentation: creating/disposing decoration types (`createDecorations`) and applying per-violation highlight ranges plus the complexity heatmap (`applyDecorations`). State is threaded in as a `DecorationSet`; the module holds no singletons.
- **`src/diagnostics.ts`** — Problems-panel presentation: maps violations to `vscode.Diagnostic` objects (`updateProblemsPanel`) — severity mapping, per-line grouping, tags, combined messages. Pure transformation; its `DiagnosticCollection` is supplied by the composition root.
- **`src/grammar.ts`** — tree-sitter grammar lifecycle: `Parser.init` (`initializeParser`) and per-language load + cache (`getOrLoadLanguage`). Holds no module state; caches live in the composition root and are injected via `GrammarContext`.
- **`src/analysis.ts`** — document analysis (`analyzeDocument`) + `.esaignore` gating (`isDocumentIgnored`). Pure orchestration over `core/*`; no presentation state.

### Analysis pipeline

`src/core/analyze.ts` (the actual current pipeline entry point) parses the active document's text into a tree-sitter AST, then runs a fixed set of independent detector passes over it, each returning `EnergyViolation[]`:

- `analyzeNesting` — flags `if`/`for`/`while`/`with` nesting deeper than 3 levels.
- `analyzeFunctionComplexity` — computes cyclomatic complexity per function, flags >10.
- `analyzeFileCoherence` — flags files with too many functions or imports (utils/helpers sprawl).
- `analyzeMagicValues` — flags "magic" numeric/string literals outside constant context.
- `analyzeParameterCount` — flags functions with >5 parameters.
- `analyzeInversionOpportunities` — flags large dominant if-blocks, nested validation chains, and deep if-nesting that could be rewritten as guard clauses / early returns.
- `extractTypeInformation` — walks the AST separately to collect function/class/variable/import type info (currently only logged; scaffolding for future features, not yet used for violations).

Each detector does its own `traverse(node)` walk of the tree-sitter tree; there's no shared visitor abstraction. The pipeline runs a larger, up-to-date set of these plus `applySuppressions` (`src/core/suppressions.ts`) as a final pass — it filters out violations covered by an `esa-ignore`/`esa-ignore-file` comment and emits low-severity `suppression` findings for directives that are unused or name an unknown type. See `docs/detectors/suppression.md`.

### Presentation

`src/decorations.ts` maps violations to `vscode.TextEditorDecorationType` ranges (color/severity: red=high, yellow=medium, green=low, rendered as background tint + gutter lightning-bolt icon via `createLightningIcon`), and `src/diagnostics.ts` mirrors the same violations into a `vscode.DiagnosticCollection` so they also show in the Problems panel.

### Violation model

`EnergyViolation { line, column, type, severity, message }`, with `type` and `severity` string-literal unions backed by the `VIOLATION_TYPE`/`SEVERITY` constant objects in `src/types.ts` (keep these two in sync when adding a new detector).

### Adding a new detector

Follow the existing pattern: write an `analyze<Thing>(tree, document): EnergyViolation[]` function that walks `tree.rootNode`, push it into the list in `src/core/analyze.ts`, and add a new `VIOLATION_TYPE` entry if it's a new category. If the violation needs special range highlighting, add a case in `applyDecorations` (`src/decorations.ts`).

### Build/packaging notes

- Webpack bundles `src/extension.ts` (the composition root) → `dist/extension.js` (CommonJS, `vscode` module treated as external).
- `web-tree-sitter`'s own `tree-sitter.wasm` is copied into `dist/` via `CopyWebpackPlugin` (webpack.config.js); the per-language grammar WASMs in `grammars/` ship separately and are loaded at runtime by path, not bundled.
- `tsconfig.json` targets ES2022/commonjs with `strict: true`.

### Coherence rules

Each file must keep ≤12 functions and ≤10 import sources (the `analyzeFileCoherence` detector). When a module approaches those limits, split by concern rather than growing it — this is what #30 did to `src/extension.ts`. The per-file domain headers in each module restate the rule at its point of use.

## Before Committing or Opening a PR

Run `just format`, `just lint`, and `just analyze` (this project dogfoods its own analyzer over `src/`) before every commit or PR, and fix what they flag. Don't rely on CI to catch formatting, lint, or energy-state violations you could have caught locally.

If satisfying `just analyze` on your change requires refactoring existing code (e.g. splitting a file to fix a coherence violation, extracting a function to fix complexity/nesting) rather than just the new code you're adding, do that refactor as its own preceding PR, merged before the PR with the actual change. Don't mix the two in one PR — a refactor bundled with a behavior change makes the diff hard to review and obscures what the change is actually about.

### Triage energy-state findings before fixing anything

`just analyze` failing on changed files is not a "refactor or suppress" choice — classify each finding first, then act. Every finding gets exactly one of these four verdicts:

- **Real violation** — the rule is right, the code is wrong. Fix it (or use an autofix). `src/extension.ts`'s coherence finding (#30) was real and fixed by splitting by domain, not by lowering a threshold — see the module map above.
- **Wrong implementation** — the detector misfires on correct code. Reproduce it with a fixture under `src/test/fixtures/` and add an integration test that the corrected detector passes; fix the *detector*, don't bend source to satisfy a buggy check. The `npm test` gate proves the claim.
- **Wrong threshold** — the rule fires on acceptable code. Adjust the threshold in the detector/config and add a boundary test at the new limit; the behavior change is proven by tests, not asserted.
- **Legitimate exception** — real, but justified here. Add an `esa-ignore` directive whose body states *why* (an ADC-style `decision:`/`invariant:`). An unused or stale directive still fails as a `suppression` finding — suppression hides nothing.

Never suppress to paper over a finding; suppression is a classified verdict that must name a real reason, not an escape hatch. Judgment lives here in prose, but the backstops are mechanical: tests gate threshold and detector changes (CI runs them), and only *used* suppressions survive.

## Releasing

Release automation runs through EasyBuild.ShipIt (see `RELEASING.md`). Use
Conventional Commit subjects (`feat:`, `fix:`, `docs:`, `chore:`, `ci:`, etc.)
for commits and PR titles — CI enforces this on PR titles, and ShipIt uses
them to generate `CHANGELOG.md` and open release PRs. Do not hand-edit
generated changelog entries or bump `package.json`'s version manually.

## Agent Decision Comments

This repository uses Agent Decision Comments.
See `AGENT_DECISION_COMMENTS.md` for the locally adopted convention.
Upstream releases: https://github.com/dbrattli/adc/releases

Before modifying code, read the ADCs already governing it.
Treat them as active constraints and justify any change explicitly.
Add ADCs for non-obvious rationale introduced by your change.
