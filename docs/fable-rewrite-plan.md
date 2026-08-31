# Fable rewrite — completed architecture

Status: **complete.** The product implementation, CLI, and tests are F#. Fable 5 compiles only
to JavaScript; TypeScript is no longer a product build or test dependency.

## Target and source layout

F# source is the only implementation source:

```text
src/Core/          host-independent detector pipeline and reports
src/Languages/     Python, F#, TypeScript, and Kotlin grammar adapters
src/Extension/     VS Code facade, presentation, grammar lifecycle, and entry point
src/Cli*.fs        CLI Node boundary, runtime, argument parser, and modes
cli/Main.fs        Fable CLI entry point
tests/             Scriptorium test suite
```

`src/test/fixtures/**/*.ts` remain analysis inputs. They are not TypeScript product or test code.
Every F# file is explicitly ordered in its `.fsproj`, as required by the F# SDK.

## Final JavaScript build graph

```text
src/EnergyState.fsproj                     cli/EnergyState.Cli.fsproj
          │                                           │
          └──── dotnet fable --lang javascript --noCache ────┐
                                                              ▼
                                                    fable-out/ (ESM)
                                      ┌───────────────────────┴──────────────────────┐
                                      ▼                                              ▼
       fable-out/extension/Extension/Extension.js              fable-out/cli/Main.js
                                      │                                              │
                                      └────────── webpack (target: node) ────────────┘
                                                              │
                                      ┌───────────────────────┴──────────────────────┐
                                      ▼                                              ▼
                                            dist/extension.js                         dist/cli.js
```

Webpack consumes Fable's ESM directly—there is no `tsc` or `ts-loader`. It keeps `vscode` as a
CommonJS external, bundles `web-tree-sitter`, copies `web-tree-sitter.wasm`, and applies the
shebang banner to `dist/cli.js`. The public `package.json` main/bin contracts remain unchanged.

`npm run fable` builds both entries into isolated ignored `fable-out/extension/` and
`fable-out/cli/` directories, so their Fable runtime modules cannot race during watch mode.
`npm run watch` first builds both bundles, then runs webpack after each completed extension
recompilation rather than while Fable is copying its runtime files.
`npm run fable-tests` emits Scriptorium tests to ignored
`fable-tests/` and writes its `{"type":"module"}` ESM shim before Node runs `Main.js`.

## Extension implementation

`Extension.fs` is the composition root. It initializes tree-sitter, owns per-activation loaded and
in-flight grammar caches, creates/disposes decorations and diagnostics, registers the command, and
wires active-editor, active-document, configuration, and close-document events.

- `Grammar.fs` lazily loads one parser per language and shares an in-flight `Task` for concurrent
  edits.
- `Analysis.fs` honors workspace `.esaignore`, supports the editor-only `includeFixtures`
  override, reads configured thresholds, clears failures instead of stale findings, and retains
  Python type-info logging.
- `ConfigurationValues.fs`, `DecorationModel.fs`, and `DiagnosticModel.fs` are pure mappings.
- `Decorations.fs` preserves category-specific ranges, lightning icons, color fallback, and
  normalized complexity heat bands.
- `Diagnostics.fs` preserves severity/tags, fixed-width ranges, same-line grouping, messages, and
  source/code values.

The narrow `Vscode*.fs` facade is the only VS Code dynamic interop boundary. `Core/TreeSitter.fs`
is the only web-tree-sitter boundary.

## CLI implementation

`cli/Main.fs` invokes `Energy.Cli.runCli`. `Cli.fs` preserves value flags and threshold overrides;
`CliModes.fs` provides the legacy one-file JSON mode, aggregate scan reports (`json`, `md`,
`human`), and `--base-ref` diff reports. The parser cache is shared for the invocation. Legacy and
scan modes block on medium/high findings; diff mode blocks only worsened file scores.

## Tests and validation

Scriptorium is the test framework. The core integration suites continue to parse the multi-language
fixtures; `ExtensionPresentationTests.fs` validates configuration mapping, decoration ranges/color
fallback/heat normalization, and Problems mapping without requiring an interactive VS Code host.
The removed TypeScript host suite did not exercise activation or presentation behavior; it is
replaced by these direct F# tests.

Run the normal gates:

```bash
just format
just lint
just test
just build
just analyze
just pack
```

`fable-out/`, `fable-tests/`, `fable_modules/`, .NET build output, source maps, sources, and tests
are excluded from distribution artifacts. The shipped packages contain transpiled bundles, WASM
assets, grammars, and public metadata.
