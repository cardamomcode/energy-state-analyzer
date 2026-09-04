# AGENTS.md

## Project overview

Energy State Analyzer is a VS Code extension and CLI that analyze Python, F#, TypeScript, Kotlin,
and C++ through `web-tree-sitter` grammars in `grammars/`. Product source is F# and is compiled
by Fable 5 to JavaScript before webpack packages the extension and CLI.

See `energy-state.md` for the original design rationale and `docs/fable-rewrite-plan.md` for the
completed migration and current build graph.

## Build commands

Prefer `Justfile` recipes:

```bash
just install       # npm install
just fable         # F# -> Fable JavaScript in fable-out/
just build         # Fable + webpack development bundles
just watch         # Fable and webpack watchers
just lint          # Fantomas check over F# source and tests
just format        # Format F# source and tests
just format-check  # CI formatting check
just md-lint       # Check markdown formatting (CI check)
just analyze       # Build, then run the F# CLI against src/ or supplied paths
just fsharp-analyze# fsharp-analyzers (G-Research rules) over .fsproj; emits SARIF for CI
just test          # Fable Scriptorium suite under Node
just pack          # Production bundles + .vsix
just clean         # Generated outputs and packages
```

Fable commands use `--lang javascript --noCache`. Fable emits ESM, so test output receives a
`{"type":"module"}` shim. Webpack consumes those ESM files and preserves the public CommonJS
contracts: `dist/extension.js`, `dist/cli.js`, and the CLI shebang.

Press `F5` to launch an Extension Development Host; `.vscode/tasks.json` runs the Fable and
webpack watch pipeline.

## Architecture

Code-quality parameters such as function and import counts are enforced by the analyzer, not by hand-written limits here — the analyzer is authoritative on them, so AGENTS.md does not set those numbers. If the analyzer flags a file, fix it (or challenge the analyzer if you believe its verdict is wrong); otherwise treat its output as final. `src/EnergyState.fsproj` has
`EnableDefaultCompileItems=false` behavior, so add every new `.fs` file explicitly in dependency
order.

- **`src/Core/`**: host-independent synchronous detector pipeline, tree-sitter facade,
  suppression, scanning, and report rendering. `Analyze.fs` is the one detector composition
  point shared by extension and CLI.
- **`src/Languages/`**: grammar-specific `LanguageAdapter` records for the five supported
  analyzed languages.
- **`src/Extension/`**: VS Code Fable facade, configuration boundary, grammar cache, analysis
  orchestration, editor/Problems presentation, and `Extension.fs` composition root. The narrow
  VS Code bindings (`Host`, `Document`, `Workspace`, `Presentation`, `Diagnostics`,
  `DiagnosticValues`, `Identity`) live in the `src/Extension/Vscode/` subfolder with their
  `Vscode*` prefix dropped from both path and module name. `DecorationModel.fs`,
  `DiagnosticModel.fs`, and `ConfigurationValues.fs` remain pure and have direct Scriptorium
  coverage.
- **`src/Cli*.fs` and `cli/Main.fs`**: CLI argument parsing, modes, runtime, narrow Node
  bindings, and Fable entry point. Scan, legacy single-file, `--report`, `--base-ref`, and
  thresholds share the Core pipeline.
- **`tests/`**: F# Scriptorium suites. `src/test/fixtures/` is retained only as multi-language
  analyzer input, including `.ts` and `.cpp` fixtures; it is not product or test implementation
  source in those languages.

The extension composition root owns lifecycle state, grammar caches, decorations, diagnostics,
commands, and editor/document/configuration event subscriptions. Presentation never leaks into
`Core/`; `Core/TreeSitter.fs` is the only web-tree-sitter facade.

`EnergyViolation` is the DU/record model in `src/Core/Violation.fs`. Add a new detector by adding
its `Detector` to `Core/Analyze.fs`, and add a `ViolationType` case plus special presentation
mapping only when required.

## Build and packaging

`npm run fable` writes generated JavaScript and `fable_modules` to ignored `fable-out/`. It first
compiles the library project for the extension entry, then the CLI project for `Main.js`.
`webpack.config.js` consumes only generated `.js`, treats `vscode` as an external, bundles
`web-tree-sitter`, copies its WASM, and emits the fixed output names. Do not add `ts-loader`,
`tsconfig.json`, or TypeScript product tooling back.

The `.vsix` and npm package ship transpiled bundles, grammar WASMs, and metadata only. Keep F#
sources, tests, `fable-out/`, `fable_modules/`, `bin/`, `obj/`, and maps excluded by ignore files.

## F# analyzers (fsharp-analyzers)

Beyond our own product analyzer, the repo runs the [fsharp-analyzers](https://g-research.github.io/fsharp-analyzers/) rule set over our written F# via a dedicated CI job (`fsharp-analyzers` in `.github/workflows/ci.yml`). It is wired through MSBuild:

- `Directory.Build.props` adds `FSharp.Analyzers.Build` (the `AnalyzeFSharpProject` target) and `G-Research.FSharp.Analyzers` (the rules), and sets `RunAnalyzers=false` so these external rules never fire during Fable transpilation — the check is driven only through the explicit target.
- `Directory.Build.targets` sets `FSharpAnalyzersOtherFlags` (`--analyzers-path`, `--code-root`, `--report`). The rule package version in that path (currently `0.23.0`) must match the SDK the rules were built against; pair it with matching tooling versions in `.config/dotnet-tools.json` (`fsharp-analyzers`), or the CLI refuses to load the analyzer DLL on an SDK-version mismatch.
- `Directory.Build.*` are evaluated for every project, so their XML must be valid: **XML comments may not contain `--`** (write "the analyzers-path flag", not `--analyzers-path` inside a comment) — otherwise every project fails MSBuild evaluation and no SARIF is produced.
- The `fsharp-analyzers` dotnet tool (0.37.2) is restored by the existing `dotnet tool restore` step; run it locally with `just fsharp-analyze [paths]`.

The job is **soft-gated** (`continue-on-error`) and uploads a SARIF per project to GitHub Code Scanning, so findings are triaged over time rather than hard-failing PRs on day one. When the backlog clears, remove `continue-on-error` from that job to make it blocking.

## Before committing or opening a PR

Run `just format`, `just lint`, `just md-lint`, and `just analyze`. Triage every analyzer finding:

- **Real violation**: fix it.
- **Wrong implementation**: add a fixture and integration test, then fix the detector.
- **Wrong threshold**: adjust the detector/configuration threshold with a boundary test.
- **Legitimate exception**: use a reasoned `esa-ignore` directive; stale directives remain
  findings.

Do not suppress a finding merely to make a check pass. If a required cleanup is unrelated to the
behavior change, land it separately rather than mixing it into the behavioral change.

## Releasing and decision comments

ShipIt release automation is described in `RELEASING.md`. Use Conventional Commit subjects; do
not hand-edit generated changelog entries or package versions.

This repository uses Agent Decision Comments. Read `AGENT_DECISION_COMMENTS.md` before modifying
governed code, preserve active `decision:` and `invariant:` comments, and add one for a
non-obvious durable design choice.
