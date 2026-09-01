# Architecture — Energy State Analyzer

Energy State Analyzer is implemented in F# and compiled with Fable 5 to JavaScript. The CLI and
VS Code extension share one host-independent detector pipeline, then webpack packages generated
ESM into the CommonJS artifacts exposed by `package.json`.

## Coherence rules

Each F# file stays at **≤12 functions** and **≤10 import sources**. The analyzer enforces the
same rule when `just analyze` dogfoods the project. Split a module by domain rather than growing
one past its responsibility.

## Module map

| Area | Owns | State |
| --- | --- | --- |
| `src/Core/` | Violation model, parser facade, detector pipeline, suppressions, scanning and reports | No host state |
| `src/Languages/` | Python, F#, TypeScript, Kotlin, and C++ `LanguageAdapter` records | Static registry |
| `src/Extension/Vscode*.fs` | Narrow Fable facade over VS Code | No |
| `src/Extension/Configuration*.fs` | VS Code settings and pure settings-to-threshold mapping | No |
| `src/Extension/Grammar.fs` | Parser initialization, loaded grammar cache, in-flight load cache | Cache supplied by root |
| `src/Extension/Analysis.fs` | Document analysis, `.esaignore`, `includeFixtures`, Python type-info logging | No |
| `src/Extension/DecorationModel.fs` / `Decorations.fs` | Pure decoration range/heatmap calculations and VS Code rendering | No |
| `src/Extension/DiagnosticModel.fs` / `Diagnostics.fs` | Pure Problems mapping and VS Code diagnostic rendering | No |
| `src/Extension/Extension.fs` | Activation, deactivation, lifecycle state, commands, events | Yes |
| `src/Cli*.fs`, `cli/Main.fs` | Argument parsing, CLI modes, Node boundary, Fable entry | Parser cache |
| `tests/` | F# Scriptorium integration and pure presentation/configuration tests | Test-only |

## Analysis pipeline

`src/Core/Analyze.fs` builds one ordered detector list over an immutable `AnalysisContext`.
Detectors remain synchronous once the source has been parsed; `Task` is confined to async edges
such as grammar loading. Suppressions run last over the combined result, so an `esa-ignore`
directive can name any detector category.

`EnergyViolation` uses F# discriminated unions for severity/type plus a record payload. The CLI
maps those values to the established JSON strings (`low`, `medium`, `high`, etc.), keeping its
public contract unchanged.

## Extension presentation

The composition root owns per-activation grammar caches, decoration types, diagnostic collection,
and subscriptions. It refreshes the active editor after editor/document/configuration events,
recreates decoration types when colors change, clears ignored/unsupported documents, and verifies
the active document again after an async grammar load.

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
