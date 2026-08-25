# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Project Overview

Energy State Analyzer is a VS Code extension that visualizes "energy states" in Python, F#, TypeScript, and Kotlin code via real-time static analysis. It parses source with `web-tree-sitter` (per-language WASM grammars in `grammars/`) and highlights code that is complex, deeply nested, or otherwise hard to maintain using editor decorations, gutter icons, and Problems-panel diagnostics.

See `energy-state.md` for the original design doc (energy-state principle, planned "detection agents", and known issues/next steps at project inception).

## Build Commands

```bash
npm run compile        # Build extension bundle via webpack (dev mode)
npm run watch          # Webpack in watch mode
npm run package        # Production build (minified, hidden source maps) — used by vscode:prepublish
npm run lint           # ESLint over src/**/*.ts
npm run compile-tests  # Compile src/test/*.ts to out/ via tsc
npm run watch-tests    # Same, in watch mode
npm run pretest        # compile-tests + compile + lint (run before test)
npm test               # Runs out/test/runTest.js (VS Code extension test host)
```

To run and debug the extension interactively, press `F5` in VS Code — this launches an Extension Development Host with the extension loaded, per `.vscode/launch.json`.

There is a single test suite (`src/test/extension.test.ts`); there's no mechanism yet to run a single test by name — use the Extension Test Runner's Testing view in VS Code, or edit the suite temporarily with `.only`.

## Architecture

Everything lives in one file, `src/extension.ts`, structured as:

1. **Activation (`activate`)** — initializes the tree-sitter `Parser`, creates decoration types, registers the `energy-state-analyzer.analyze` command, and wires up editor/document change listeners to re-analyze on the fly. Each language's grammar (`grammars/tree-sitter-<language>.wasm`, path resolved via `context.extensionPath`) is loaded lazily on first use of that language, not up front — see `getOrLoadLanguage`. Activation is gated by the `onLanguage:*` entries in `package.json` (python, fsharp, typescript, kotlin).
2. **Analysis pipeline (`analyzeDocument`)** — parses the active document's text into a tree-sitter AST, then runs a fixed set of independent detector passes over it, each returning `EnergyViolation[]`:
   - `analyzeNesting` — flags `if`/`for`/`while`/`with` nesting deeper than 3 levels.
   - `analyzeFunctionComplexity` — computes cyclomatic complexity per function, flags >10.
   - `analyzeFileCoherence` — flags files with too many functions or imports (utils/helpers sprawl).
   - `analyzeMagicValues` — flags "magic" numeric/string literals outside constant context.
   - `analyzeParameterCount` — flags functions with >5 parameters.
   - `analyzeInversionOpportunities` — flags large dominant if-blocks, nested validation chains, and deep if-nesting that could be rewritten as guard clauses / early returns.
   - `extractTypeInformation` — walks the AST separately to collect function/class/variable/import type info (currently only logged; scaffolding for future features, not yet used for violations).
   Each detector does its own `traverse(node)` walk of the tree-sitter tree; there's no shared visitor abstraction.
3. **Presentation** — `applyDecorations` maps violations to `vscode.TextEditorDecorationType` ranges (color/severity: red=high, yellow=medium, green=low, rendered as background tint + gutter lightning-bolt icon via `createLightningIcon`), and `updateProblemsPanel` mirrors the same violations into a `vscode.DiagnosticCollection` so they also show in the Problems panel.
4. **Violation model** — `EnergyViolation { line, column, type, severity, message }`, with `type` and `severity` string-literal unions backed by the `VIOLATION_TYPE`/`SEVERITY` constant objects near the top of the file (keep these two in sync when adding a new detector).

### Adding a new detector

Follow the existing pattern: write an `analyze<Thing>(tree, document): EnergyViolation[]` function that walks `tree.rootNode`, push it into the list in `analyzeDocument`, and add a new `VIOLATION_TYPE` entry if it's a new category. If the violation needs special range highlighting, add a case in `applyDecorations`.

### Build/packaging notes

- Webpack bundles `src/extension.ts` → `dist/extension.js` (CommonJS, `vscode` module treated as external).
- `web-tree-sitter`'s own `tree-sitter.wasm` is copied into `dist/` via `CopyWebpackPlugin` (webpack.config.js); the per-language grammar WASMs in `grammars/` ship separately and are loaded at runtime by path, not bundled.
- `tsconfig.json` targets ES2022/commonjs with `strict: true`.

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
