# Architecture — Energy State Analyzer

Energy State Analyzer is implemented in F# and compiled with Fable 5 to JavaScript. The CLI and
VS Code extension share one host-independent detector pipeline, then webpack packages generated
ESM into the CommonJS artifacts exposed by `package.json`.

## Reading path

Start with [Analyze](../src/Core/Analyze.fs) and
[DetectorPipeline](../src/Core/DetectorPipeline.fs) for the shared transformation. Read
[LanguageAdapter](../src/Core/LanguageAdapter.fs) with one adapter in `src/Languages/` to see how
grammar facts enter detectors. Then follow [Cli](../src/Cli.fs) → [CliModes](../src/CliModes.fs) →
[CliRuntime](../src/CliRuntime.fs), or [Extension](../src/Extension/Extension.fs) →
[Grammar](../src/Extension/Grammar.fs) → [Analysis](../src/Extension/Analysis.fs) for the two host
flows. The analyzer itself determines code-quality thresholds; this guide sets none.

## Module map

| Area | Owns | State |
| --- | --- | --- |
| `src/Core/` | Violation model, parser facade, detector pipeline, suppressions, scanning and reports | No host state |
| `src/Languages/` | Grammar-specific `LanguageAdapter` records for the supported languages | Static registry |
| `src/Extension/Vscode/` | Narrow Fable facade over VS Code | No |
| `src/Extension/Configuration*.fs` | VS Code settings and pure settings-to-threshold mapping | No |
| `src/Extension/Grammar.fs` | Parser initialization, loaded grammar cache, in-flight load cache | Cache supplied by root |
| `src/Extension/Analysis.fs` | Document analysis, `.esaignore`, and `includeFixtures` | No |
| `src/Extension/DecorationModel.fs` / `Decorations.fs` | Pure decoration range/heatmap calculations and VS Code rendering | No |
| `src/Extension/DiagnosticModel.fs` / `Diagnostics.fs` | Pure Problems mapping and VS Code diagnostic rendering | No |
| `src/Extension/Extension.fs` | Activation, deactivation, lifecycle state, commands, events | Yes |
| `src/Cli*.fs`, `cli/Main.fs` | Argument parsing, CLI modes, Node boundary, Fable entry | Parser cache |
| `tests/` | F# Scriptorium integration and pure presentation/configuration tests | Test-only |

## Analysis pipeline

`src/Core/DetectorPipeline.fs` orders detectors over an immutable `AnalysisContext`;
`src/Core/Analyze.fs` creates the context and applies suppressions after detection. The shared
`src/Core/ParsedAnalysis.fs` owns the parsed tree's lifetime and converts parse or detector
exceptions into typed errors. Detection stays synchronous; grammar loading is asynchronous.
Applying suppressions last lets an `esa-ignore` directive name any detector category.

`EnergyViolation` uses F# discriminated unions for severity/type plus a record payload. The CLI
maps those values to the established JSON strings (`low`, `medium`, `high`, etc.), keeping its
public contract unchanged.

`src/Core/Config.fs` resolves a project's optional `.esaconfig.json` before analysis. Missing
discovered config uses built-in defaults; a selected file that cannot be read or parsed is an
error. The CLI applies only supplied flag overrides to those resolved options. CLI paths are
checked before scanning, while host-specific source, grammar, parse, and report-write failures
leave the analysis path as explicit errors.

## Extension presentation

The composition root owns per-activation grammar caches, decoration types, diagnostic collection,
and subscriptions. It refreshes the active editor after editor/document/configuration events,
recreates decoration types when colors change, clears ignored/unsupported documents, and verifies
the active document again after an async grammar load.
Concurrent requests share an in-flight grammar load; completion removes that task so a failed
load can be retried by the next request.

`DecorationModel.fs` keeps category-specific range selection and per-violation normalized
complexity heatmaps testable outside VS Code. `DiagnosticModel.fs` groups same-line findings into
one Problems entry, preserving severity ordering, tags, codes, and combined messages.

## Build and packaging

`npm run fable` invokes Fable with `--lang javascript --noCache`, emitting ignored ESM into
`fable-out/`. `webpack.config.js` consumes:

- `fable-out/extension/Extension/Extension.js` → `dist/extension.js`
- `fable-out/cli/Main.js` → `dist/cli.js`

Webpack keeps `vscode` external, bundles `web-tree-sitter`, copies `web-tree-sitter.wasm`, and
adds the CLI shebang. Per-language grammar WASMs remain runtime files in `grammars/`. There is no
TypeScript compiler or loader in the product build.

The registry is keyed by VS Code language ID. CLI filenames are matched against a longest-first,
case-insensitive suffix table, which is required for compound C++ suffixes such as `.hpp.in` and
keeps C, CUDA, and Objective-C++ outside the C++ adapter. Bundled third-party grammar artifacts keep
their provenance, checksum, and license notice beside the WASM.

Rust `.rs` files use the same registry and parser pipeline. The Rust adapter maps syntax from
`tree-sitter-rust` to the shared detector contracts, including function and closure views,
typed parameters, `use` imports, and multi-arm `match` decisions.
