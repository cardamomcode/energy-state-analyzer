# Plan — Rewrite Energy State Analyzer in F# (Fable → JavaScript)

Status: **Phase 0 spike in progress.** Scope: full in-repo rewrite of `src/` from
hand-written TypeScript to idiomatic F#, compiled with **Fable 5**, tested with
**Scriptorium** (Quill runner + Nib assertions), with the analyzer expressed as an
explicit **pipeline** of pure detector passes.

> **Build target is JavaScript, not TypeScript.** The plan originally targeted Fable's
> `--lang typescript`. During Phase 0 we hit a TS-target bug (see below) and switched to
> the **JS target (`--lang javascript`)** — user-approved. See the **Current status**
> section for what is done, what remains, and the gotchas learned. The webpack-consumes-JS
> portion of the build graph (§3.6) is now **proven on the JS target** (Phase 0: Fable's
> emitted `.js` is a valid webpack entry with `vscode` external + web-tree-sitter bundled);
> only the formal prose rewrite of §3.6 to the JS-target shape remains, mechanically
> validated already. The F# layout in §3.1 is unchanged.

## Current status (Phase 0, updated)

### Target decision changed: JavaScript, not TypeScript
The plan originally targeted Fable's `--lang typescript`. During Phase 0 we hit a
**Fable TS-target bug**: `[<Emit("$0.property")>]` property-access bindings fail to parse
("Unexpected symbol ']' in attribute list", F# code 10). The full write-up and the prompt
for fixing it live in **`docs/fable-ts-target-task-bug-prompt.md`**.

**Decision (user-approved): use the JS target (`--lang javascript`) instead.** Fable emits
plain JS directly — no `tsc`, no `.fs.ts`, no tsconfig retargeting. This sidesteps the TS
bug entirely for now. Consequence: §3.1 and §3.6 (which describe the `.fs.ts` + tsc build
graph) are stale *for the target* — they still document the F# layout correctly, but the
build graph needs a JS-target revision when we get there. The "ship transpiled only"
policy (§3.7) is unchanged in spirit: we ship `.js` instead of `.fs.ts`.

### Phase 0 progress

| Plan item | Status | Evidence |
|---|---|---|
| Add fable 5.15.0 + fantomas to `.config/dotnet-tools.json`; `just setup` restores | ✅ done | pinned with `rollForward: false` (fantomas 7.0.6 also) |
| Tiny F# project (record/DU/`task { }` awaiting a JS promise) + Scriptorium test bridging via `toAsync`/`Async.AwaitTask`, asserting on it | ✅ done | `src/Core/Hello.fs` + `tests/Main.fs`; 4 tests pass under `node spike-js/Main.js` |
| Prove fable → node runs Scriptorium with a correct exit code (pass **and** fail paths) | ✅ done | **Both via `just spike`.** Pass: 4 passed, exit 0. Fail: a temporary `isGreaterOrEqual 99` assertion in `tests/SpikeTests.fs` → exit 1, then reverted to `2`. |
| Nail the web-tree-sitter binding syntax; parse one Python fixture and print the root node type | ✅ done | `tests/SpikeTests.fs`: parses a Python fixture and asserts the root node type — passing. Named-import bindings (`[<Import("Parser"/"Language", "web-tree-sitter")>]`) + `[<Emit("$0.method()")>]` for calls work; property accessors use `[<Emit("$0.property")>]`. |
| Prove webpack consumes generated JS as an entry with `vscode` external and the web-tree-sitter import resolves in the bundle | ✅ done | Temp config `webpack.spike.config.js` (mirrors the real extension shape) bundled `spike-js/Main.js` + a vscode probe: `web-tree-sitter.js` (150 KiB) **and** its WASM are *bundled*, `vscode` is an `external`, and `node dist-spike/test.js` runs the suite → 4 passed, exit 0. No ts-loader/tsc — Fable's emitted `.js` is a valid webpack entry on the JS target. Temp files removed after proof (Phase 0 item 5). |

### Phase 0 closed (all items done)

All five Phase 0 items are complete; `just spike` is green and the scratch artifacts are cleaned up. Exit criteria from §4, met:

| Exit criterion | Status | Evidence |
|---|---|---|
| `just spike` green — fable→node with a correct exit code on **both** pass and fail paths | ✅ | `just spike` → 4 passed, exit 0; temp failing assertion → exit 1 (reverted). |
| web-tree-sitter binding pattern documented in `src/Core/TreeSitter.fs` | ✅ | Named imports + `[<Emit("$0.method()")>]` / `[<Emit("$0.property")>]`; the `parseWith` convenience chain. |
| Generated-file git policy decided & applied | ✅ | `.gitignore` now ignores `bin/`, `obj/`, `fable_modules/`, `spike-js/` (the JS-target outDir holding `<name>.js` + fable_modules `.fs.js`). Repo's "F# is source of truth" stays clean. |
| webpack proof — generated JS consumed as an entry with `vscode` external, web-tree-sitter resolves in the bundle | ✅ | Temp `webpack.spike.config.js`: web-tree-sitter.js+WASM bundled, `vscode` external, suite runs from the bundle (exit 0). No ts-loader/tsc. |
| Scratch cleaned up | ✅ | `_repro/`, `_ts/`, `spike-js/`, `tsconfig.spike.json`, and the temp webpack config/output removed. |

**What this de-risks for Phase 1:** the entire build/test chain is proven — Fable 5 (JS target) emits plain JS, Node runs Scriptorium with a real CI exit code, web-tree-sitter binds end-to-end, and webpack consumes the emitted JS exactly as it will consume the real `src/` output. The remaining work is porting detectors + languages to F# (§3.2–§3.5), which is Phase 1.

> **Open follow-up (not blocking):** §3.6's build-graph prose still describes the original TS-target shape (`*.fs.ts` + `tsc`). The JS-target *mechanics* are proven above; rewriting §3.6 to the JS-target graph and §3.1's generated-file note is a docs pass for Phase 4, not a Phase 0 blocker.

### Gotchas learned (for the next session)
- **Fable cache instability:** intermittent "Unexpected symbol ']' in attribute list" parse errors on `[<Emit>]` attributes; resolved by cleaning `bin/obj` and/or passing `--noCache`. The current tree compiles clean.
- **JS-target output naming is inconsistent:** our own sources emit `<name>.js` (e.g. `src/Core/Hello.fs` → `spike-js/src/Core/Hello.js`) while fable_modules packages keep `.fs.js` (e.g. `Quill.fs.js`). Verify the exact import paths when wiring webpack.
- **Node ESM:** Fable emits ESM; add a top-level `{"type":"module"}` to the output dir's `package.json` so `node spike-js/Main.js` runs without "ERR_REQUIRE_ESM".
- **webpack + Fable JS (proven):** bundling the emitted `.js` needs no ts-loader — point webpack at the entry `.js`, keep `vscode` in `externals`, and the existing `asset/resource` wasm rule copies `web-tree-sitter.wasm` into the bundle exactly as for TS. The suite runs from the bundled output with a correct exit code. Entry filenames are Fable's `<name>.js`; fable_modules packages stay `.fs.js`.
- **F# SDK quirk:** `EnableDefaultCompileItems=false` in the fsproj means you must list `<Compile Include=...>` explicitly (the existing repo pattern).

## 1. Goals and non-goals

**Goals**
- All product code (detectors, pipeline, CLI, VS Code extension) in F#, idiomatic:
  records, discriminated unions, options, module functions, no classes/`any` in our own code.
- F# → Fable 5 (JS target) → JS, bundled by the existing webpack pipeline. No standalone
   `tsc` step — Fable emits plain JS directly (see Current status).
  Public contracts unchanged: `dist/extension.js` (extension main), `dist/cli.js` (npm `bin`),
  `package.json` contributes/activationEvents, `action.yml` CLI usage, ShipIt releases.
- Scriptorium as the single test framework, replacing mocha for all F# tests.
- The analyzer runs as a **pipeline**: a list of named detector passes over a shared
  immutable context, with suppression as the final pass — replacing the current
  `violations.push(...)` accumulator in `src/core/analyze.ts`.
- Continue dogfooding: `just analyze` runs the (now F#) analyzer over our own F# sources
  (the F# grammar and F# fixtures already exist).

**Non-goals**
- No new detectors, no behavior changes. This is a rewrite, not a feature PR.
  (Per AGENTS.md: any required behavior change is a separate PR with tests.)
- No changes to the web-tree-sitter grammars in `grammars/` or the `web-tree-sitter` runtime.
- No migration to a new bundler (webpack stays — see §7 decisions).

## 2. Stack facts (verified 2026-08)

| Fact | Evidence |
|---|---|
| Fable 5 stable (2026-04-21); current line is Fable 5, `Fable.Core 5.2.0`; **latest release is Fable 5.15.0** (NuGet `Fable`) — the version we pin | `Fable.Core` CHANGELOG 5.0.0 entry; Scriptorium nuspec deps; nuget.org/packages/Fable/5.15.0 |
| Fable is a dotnet tool: `dotnet fable [watch\|clean]` | fable.io CLI docs |
| **TypeScript target is marked stable**: `dotnet fable --lang typescript` emits `X.fs.ts` next to `X.fs` | fable.io CLI options (`--lang ... typescript (alias ts) - stable`) |
| Generated TS is compiled with plain `tsc`; the quick-start adds **no runtime npm dependency** — the Fable runtime is inlined into the generated TS | fable.io "Getting started / TypeScript" (only `typescript` + `@types/node` installed) |
| Scriptorium targets Fable 5: `Scriptorium.Quill 0.7.0` depends on `Fable.Core 5.2.0`, `FSharp.Core 10.x`; packages: Quill (runner), Nib (assertions), Ink (ANSI), Parchment (logging), Nib.Snapshot, Nib.Browser, Hedgehog | NuGet nuspec; fable-hub/Scriptorium README |
| Quill runs natively under JS/TS: JS path uses `setTimeout` for async timeouts and finishes with `process.exit(exitCode)`; `Compiler.isTypeScript` branches exist in the runner itself | `src/Scriptorium.Quill/Quill.fs` (fable-hub/Scriptorium) |
| Scriptorium pins `fable 5.15.0` as a dotnet tool and formats with Fantomas | Scriptorium `dotnet-tools.json` |
| Fable 5 JS-interop primitives: `jsNative`, `[<Import>]`/`[<ImportMember>]`/`[<ImportDefault>]`, `[<Emit>]`, `emitJsExpr`/`emitJsStatement`, `createObj`, dynamic typing, `Fable.Node` for Node APIs | fable.io "JavaScript / Features" |
| **`Task`/`task { }` and `Async` are different in Fable 5.** F#'s task computation expression (`task { }`, `System.Threading.Tasks.Task`) maps **directly to native JS `Promise<T>`** on the js/ts targets ("Map task { } to Promise<T>", fable-compiler 2.4.0, commit `97f54d3`) — `let!` inside `task { }` is a native `await`. F#'s `async { }` (`Async<'T>`) is Fable's own event-loop implementation (separate from promises; `Async.RunSynchronously` unsupported) and bridges to tasks via `Async.AwaitTask` | Fable `fable-compiler-js` CHANGELOG 2.4.0; `tests/Js/Main/TaskTests.fs` (bridge comment: "works on .NET (Async.AwaitTask wraps Task) and JS (awaitPromise wraps Promise)") |
| Consequence for us: **promise-based JS APIs (web-tree-sitter) get bindings declared as `Task<'T>`**, awaited directly in `task { }` blocks; Scriptorium Quill async test bodies are `Async<unit>` (see next row), so tests bridge with `Async.AwaitTask` (Fable.Giraffe's helper: `let toAsync (t: Task<'a>) : Async<'a> = Async.AwaitTask t`) | `tests/Js/Main/TaskTests.fs`; Scriptorium `src/Scriptorium.Quill/Types.fs` (`AsyncTest of TestDefinition<TestContext -> Async<unit>>`); Fable.Giraffe `test/shared/Helpers.fs:20` |
| Quill test bodies: sync = `TestContext -> unit`, async = `TestContext -> Async<unit>` (F# `Async`, not `Task`); the working idiom is `testAsync (name, fun _ -> toAsync (task { ... }))` | Scriptorium `Types.fs`/`DSL.fs` (`testAsync ... body: Async<unit>`); Fable.Giraffe `test/shared/HandlerTests.fs` |
| **A local working reference exists: the sibling `Fable.Giraffe` repo** (same author stack) uses Fable 5 + Scriptorium + `task { }` throughout — its `src/Core.fs` (Task-based handler pipeline, `return!` delegation), `src/js/HttpContext.fs` (promise-API bindings as `Task<'T>`), `test/js/Main.fs` (`[<EntryPoint>] let main _ = runTests [...]` with the note that on JS "Quill cannot block, so runTests returns 0 immediately and chains process.exit onto the resolved promise"), and `test/shared/*` (Scriptorium async tests) are the patterns to copy | `~/developer/Fable.Giraffe` (local checkout) |
| Performance note from that codebase: a `task { }` block has real builder overhead (builder/Delay/Run) — Fable.Giraffe `Core.fs:99`: "Return the existing Task directly; a `task { return! ... }` wrapper here is pure per-request overhead since neither branch awaits." → pass `Task` values straight through; only open a `task { }` when you actually `let!` | Fable.Giraffe `src/Core.fs:99-104` |
| **No existing Fable binding for web-tree-sitter** (GitHub repo search: 0; npm: none) — we write a small binding layer | GitHub/npm searches |
| web-tree-sitter 0.26 API surface used: `Parser.init()`, `Language.load(path)` (both promise-based), `parser.setLanguage`, `parser.parse(text)`, node: `type`/`text`/`startPosition`/`endPosition`/`namedChildren`/`child(i)`/`parent`/`rootNode` | existing TS sources |

## 3. Target architecture

### 3.1 Repo layout (final state)

```
energy-state-analyzer/
├── src/                          # F# source (replaces the current .ts files)
│   ├── Core/
│   │   ├── Violation.fs          # Severity, ViolationType, EnergyViolation, Hotspot
│   │   ├── Context.fs            # AnalysisContext record
│   │   ├── Analyze.fs            # Detector type + pipeline + suppression final pass
│   │   ├── Position.fs           # (port of core/position.ts)
│   │   ├── Esaignore.fs          # (port of core/esaignore.ts)
│   │   ├── Suppressions.fs       # (port of core/suppressions.ts)
│   │   ├── Scan.fs               # (port of core/scan.ts)
│   │   ├── Report.fs / ReportDiff.fs / ReportHuman.fs   # (ports)
│   │   ├── NamingCohesion.fs / TypeCohesion.fs / ClassRelatedness.fs
│   │   ├── LanguageAdapter.fs    # NodeTypes + LanguageAdapter records
│   │   ├── TreeSitter.fs         # Fable binding for web-tree-sitter
│   │   └── Detectors/            # Nesting.fs, Cyclomatic.fs, Cognitive.fs, Coherence.fs,
│   │                             # MagicNumber.fs, MagicString.fs, ParameterCount.fs,
│   │                             # Inversion.fs, MatchOpportunity.fs, LogicalControlFlow.fs,
│   │                             # OpaqueBoolean.fs, PrimitiveObsession.fs
│   ├── Languages/                # Python.fs, FSharp.fs, TypeScript.fs, Kotlin.fs, Registry.fs
│   ├── Cli/                      # Cli.fs (entry), CliModes.fs
│   └── Extension/                # Extension.fs (activate/deactivate entry), Decorations.fs,
│                                 # Diagnostics.fs, Grammar.fs, Vscode.fs (vscode binding)
├── tests/                        # Fable test project (F# only)
│   ├── Core/…Tests.fs            # one test module per current src/test/integration/*.test.ts
│   ├── ExtensionTests.fs         # the vscode-host suite (decorations/diagnostics wiring)
│   └── Main.fs                   # [<EntryPoint>] main _ = Test.runTests [ ... ]
├── grammars/                     # unchanged
├── .config/dotnet-tools.json     # + fable 5.15.0, fantomas
├── .fsproj files                 # src/EnergyState.fsproj, tests/EnergyState.Tests.fsproj
├── tsconfig.json                 # now compiles the *generated* .fs.ts files
├── webpack.config.js             # entry points retarget to generated TS (same outputs)
└── package.json                  # main/bin/contributes unchanged; scripts retarget
```

Fable emits `src/**/*.fs.ts` and `tests/**/*.fs.ts` **next to the sources** (its default;
`-e/--extension` controls the suffix). `.fs.ts` files are treated as generated: gitignored
or committed depending on the spike's verdict (see Phase 0), excluded from formatting/lint,
and listed in `.esaignore`.

### 3.2 Domain model — idiomatic F#

Discriminated unions replace the string-literal unions of `src/types.ts` (the wire-format
strings for the CLI JSON contract become a tiny `ViolationType -> string` mapping):

```fsharp
module Energy.Core

type Severity = Low | Medium | High

type ViolationType =
    | Nesting | Complexity | Cognitive | Naming | Coherence | Magic
    | Parameters | Inversion | PrimitiveObsession | MatchOpportunity
    | LogicalControlFlow | OpaqueBoolean | Suppression

type Hotspot = { Line: int; Weight: int }

type EnergyViolation = {
    Line: int
    Column: int
    Type: ViolationType
    Severity: Severity
    Message: string
    Hotspots: Hotspot list          // list, not array — no Option/empty-array ceremony
}
```

The current 25-hook `LanguageAdapter` interface (a `class`-less interface of predicate
callbacks) becomes a **record of functions** — the idiomatic F# shape (data + behavior,
no interface ceremony):

```fsharp
type NodeTypes = {
    Block: string option            // Option instead of `string | null`
    Parameters: string
    IfStatement: string option
    // ... one field per current LanguageNodeTypes member
}

type LanguageAdapter = {
    Id: string
    GrammarPath: string
    NodeTypes: NodeTypes
    IsFunctionDefinition: TreeSitter.Node -> bool
    GetBooleanOperator: TreeSitter.Node -> BooleanOperator option
    ExtractTypedParameter: TreeSitter.Node -> TypedParam option
    // ... remaining hooks; null-returning hooks become `... option`
}
```

### 3.3 The pipeline

One immutable context replaces the repeated `(tree, positions, language, fileName)`
parameter quadruple (which is exactly the swap-risk shape this repo's own
primitive-obsession detector flags):

```fsharp
type AnalysisContext = {
    Source: string
    Tree: TreeSitter.Node            // root node
    Language: LanguageAdapter
    FileName: string
    Thresholds: Thresholds           // record, all fields have defaults — no `?? DEFAULT_*` at call sites
}

type Detector = { Name: string; Run: AnalysisContext -> EnergyViolation list }
```

`Core/Analyze.fs` becomes the single composition point (preserving the existing ADC:
both entry points — extension and CLI — share this exact list):

```fsharp
let allDetectors : Detector list = [
    Detectors.Nesting.detector
    Detectors.Cyclomatic.detector
    Detectors.Cognitive.detector
    Detectors.Coherence.detector
    Detectors.MagicNumber.detector
    Detectors.MagicString.detector
    Detectors.ParameterCount.detector
    Detectors.Inversion.detector
    Detectors.PrimitiveObsession.detector
    Detectors.MatchOpportunity.detector
    Detectors.LogicalControlFlow.detector
    Detectors.OpaqueBoolean.detector
]

/// decision: suppression runs last over the full combined list — an esa-ignore directive
/// can name any violation type regardless of which detector produced it (ported ADC).
let runPipeline : AnalysisContext -> EnergyViolation list =
    fun ctx ->
        ctx
        |> allDetectors
        |> List.collect (fun d -> d.Run ctx)
        |> Suppressions.apply ctx.Source
```

Properties this buys over the current `violations.push(...)` loop:
- Detectors are **pure functions** `(AnalysisContext -> EnergyViolation list)` — trivially
  testable, no module state, no ordering coupling.
- Adding a detector = one line in `allDetectors` (the "adding a new detector" rule in
  AGENTS.md becomes literally that).
- Subsets are trivial: `runPipeline` over a filtered list is how per-detector on/off
  (magicNumber/magicString today) generalizes without a config matrix.
- Each detector module exposes both its `detector` value and its threshold record, so
  CLI flag parsing (`--medium-nesting` etc.) and the VS Code settings reader map into the
  same `Thresholds` record from one place.

**Async boundary of the pipeline.** `runPipeline` and every detector stay **plain
synchronous** F# functions — the tree is already parsed, so no `task { }` is involved at
all. Asynchrony lives only at the edges, and there it uses `Task` (native promises),
mirroring Fable.Giraffe's handler pipeline (`Core.fs`'s `task { let! ... }` composition
with `return!` delegation is the local reference for that shape):

```fsharp
// Edge — parsing (Core/TreeSitter.fs, see §3.4 for the binding sketch).
// Task = native Promise; only grammar load + parser init are async JS calls.
// Mirrors the current TS: the caller parses first, then analyzeSource gets the tree.
let parseWith (language: LanguageAdapter) (source: string) : Task<TreeSitter.Node> =
    task {
        do! TreeSitter.init ()                    // Parser.init() — promise
        let! lang = TreeSitter.loadLanguage language.GrammarPath   // Language.load — promise
        let parser = TreeSitter.makeParser lang   // setLanguage + new Parser — sync
        return TreeSitter.parse parser source      // parser.parse — sync
    }

// Core — fully synchronous (port of the current analyzeSource signature):
let analyzeSource (language: LanguageAdapter) (tree: TreeSitter.Node)
    (source: string) (fileName: string)
    : EnergyViolation list =
    runPipeline { Source = source; Tree = tree; Language = language
                 FileName = fileName; Thresholds = defaultThresholds }

// Entry points compose the edge + core:
//   extension: activate = task { let! tree = parseWith lang src; ... apply tree }
//              (returns a native promise — what the VS Code host awaits)
//   CLI:       main = task { let! tree = parseWith lang src; ... print }
```

Rule of thumb (from Fable.Giraffe `Core.fs:99`): when a function merely forwards an
existing `Task` without awaiting anything, **return it directly** — don't wrap it in
`task { return! ... }`, which adds builder/Delay/Run overhead. Only open a `task { }`
block where you actually `let!`/`do!`.

### 3.4 Bindings (the only imperative/interop surface)

**`Core/TreeSitter.fs`** — thin typed facade over `web-tree-sitter` (0.26):

```fsharp
module TreeSitter

open Fable.Core
open Fable.Core.JsInterop

[<ImportMember("web-tree-sitter", "Parser")>]
let parserModule : obj = jsNative

[<ImportMember("web-tree-sitter", "Language")>]
let languageModule : obj = jsNative

// Fable 5 maps task { } / Task<'T> directly to native Promise<T> (js/ts targets),
// so promise-based JS APIs are bound as Task and awaited with plain let!/do!
// inside task { } blocks — no wrappers. (Async<'T> is Fable's separate
// event-loop implementation; it bridges to Task via Async.AwaitTask.)
let init () : Task<unit> = jsNative
let loadLanguage (path: string) : Task<Language> = jsNative
let parse (parser: Parser) (source: string) : Node = jsNative

type Node = {
    Type: string
    Text: string
    StartPosition: { Row: int; Column: int }
    EndPosition: { Row: int; Column: int }
    Parent: Node option
    NamedChildren: Node list
    // ...
}
```

(The async story is settled: `task { }`/`Task<'T>` maps to native `Promise<T>` on the
js/ts targets, so the `Task` signatures above are the real shape and `task { let! ... }`
is native `await`. Code that lives in the `Async` world — Scriptorium test bodies,
below — bridges via `Async.AwaitTask`, the same pattern Fable's own `TaskTests.fs`
uses. The only open detail for the Phase 0 spike is the module-import syntax:
web-tree-sitter is a CommonJS module, so `[<ImportAll>]`/dynamic member access is the
expected path; the spike decides and we keep whichever is cleanest.)

**`Extension/Vscode.fs`** — `[<Import("vscode")>]` binding covering the ~20 API members
actually used by the current extension (`window.activeTextEditor`,
`window.onDidChangeActiveTextEditor`, `window.showInformationMessage/showErrorMessage`,
`workspace.onDidChangeTextDocument/onDidChangeConfiguration`,
`workspace.getConfiguration`, `languages.createDiagnosticCollection`,
`commands.registerCommand`, `Position/Range/Diagnostic/DiagnosticSeverity`,
`TextDocument.getText/fileName/languageId/uri`, `TextEditor`, `ExtensionContext`,
`Disposable`). `vscode` stays a webpack CommonJS external — the generated TS
`import ... from 'vscode'` resolves identically today.

**Node APIs** (`fs`/`path`/`process`) via `Fable.Node` (official Fable 5 bindings) for
CLI + scan + esaignore, instead of hand-rolled `Emit` bindings.

### 3.5 Tests — Scriptorium

One Fable test project (`tests/`), F# only, no mocha:

- **Core integration suites** — port each `src/test/integration/*.test.ts` 1:1 into an
  F# test module, reusing the **same fixture files** (`src/test/fixtures/**`):
  `Test.test "flags nesting deeper than 3" (fun () -> assertThat violations (contains ...))`
  with Nib fluent assertions (`isEqualTo`, `isGreaterOrEqual`, `inside _.Line ...`,
  tags). The `testUtils.ts` helpers (`parseFixture`, `findFunctionRange`,
  `violationsIn`, `assertValidPositions`) become `Tests/TestUtils.fs`.
- **Async tests** — Quill's async test bodies are `Async<unit>` (`Test.testAsync ...
  body: Async<unit>` — F#'s `async`, confirmed in Quill `DSL.fs`) and the runner drives
  them natively under JS with JS-side timeouts. Our tree-sitter bindings return `Task`
  (native promises), so test bodies bridge with `Async.AwaitTask` — exactly the pattern
  in Fable's own `TaskTests.fs` **and in the working local reference
  `Fable.Giraffe`** (same author stack):

  ```fsharp
  // Tests/Helpers.fs — copied from Fable.Giraffe test/shared/Helpers.fs
  let toAsync (t: Task<'a>) : Async<'a> = Async.AwaitTask t

  testAsync (
      "flags nesting deeper than 3",
      fun _ ->
          toAsync (
              task {
                  let! { Source; Tree } = parseFixture PYTHON "python/nesting.py"
                  let violations =
                      runPipeline { Source = Source; Tree = Tree; Language = PYTHON
                                FileName = "nesting.py"; Thresholds = defaultThresholds }
                  assertThat violations (containsType Nesting)
              }
          )
  )
  ```

  No mocha-style `await` shims. Entry point, copied from `Fable.Giraffe test/js/Main.fs`
  (on JS "Quill cannot block, so `runTests` returns 0 immediately and chains
  `process.exit` onto the resolved promise"):

  ```fsharp
  [<EntryPoint>]
  let main _ =
      runTests [ NestingTests.tests; CyclomaticTests.tests; ... ]
  ```

  (Extension-side code — `activate`, grammar loading — stays in the `task { }` world
  where it's a native promise, which is what the VS Code extension host expects from an
  async `activate`. Under Node the Quill runner chains `process.exit(exitCode)` onto the
  resolved promise — so `node tests/main.fs.js` is a self-contained test command with a
  proper CI exit code.)
- **Extension-host suite** (decorations/diagnostics wiring, currently
  `src/test/extension.test.ts`) — written in F# against the `Vscode.fs` binding, run
  inside the VS Code Extension Development Host. Strategy (Phase 3 spike, with fallback):
  1. **Primary**: point `@vscode/test-cli` at the compiled test bundle; the Quill
     runner executes on load and exits the host with the test exit code (mechanically
     compatible: `process.exit` inside the extension host terminates the host, the outer
     runner reports that code).
  2. **Fallback**: a ~20-line hand-written mocha adapter (one `it`) that calls the same
     Quill test list from F# — keeps Scriptorium as the framework (all test *code* is
     F#/Nib) while borrowing mocha only as the host-process carrier.
- **Property-based** (optional, low cost): `Scriptorium.Hedgehog` for the suppression
  parser and esaignore glob rules in Phase 1 — both are pure string→result functions
  with lots of edge cases.

### 3.6 Build graph (final state)

```
F# (src/, tests/)
   │  dotnet fable --lang typescript        (dotnet tool, fable 5.15)
   ▼
*.fs.ts (generated, colocated)
   │  tsc (existing tsconfig, retargeted)   or ts-loader inside webpack (Phase 0 decides)
   ▼
*.js
   │  webpack (unchanged config shape: target node, vscode external, wasm asset copy,
   │  BannerPlugin shebang for cli, dist/extension.js + dist/cli.js outputs)
   ▼
dist/extension.js, dist/cli.js, dist/web-tree-sitter.wasm   ← package.json contract unchanged
```

- `tsconfig.json` keeps `strict: true`, `ES2022`; `include` retargets to the generated TS
  (and excludes fixtures); generated files get `"noEmitOnError": false` discipline only —
  we never edit generated code.
- webpack entry paths change from `./src/extension.ts` → the generated entry TS
  (e.g. `./src/extension.fs.ts`); everything else (externals, CopyPlugin, BannerPlugin)
  stays. If Phase 0 shows the generated TS is large/slow under ts-loader, the alternative
  is tsc-to-JS first, webpack bundling plain JS — same outputs.
- `just` recipes (final): `install`, `fable` (transpile), `build` (fable+webpack),
  `watch`, `test` (fable + tsc + node tests + host tests), `analyze`, `format`
  (fantomas over `src/**/*.fs`), `lint` (fantomas check), `pack`, `pack-check`,
  `clean` (adds `*.fs.ts`, `fable_modules`, `bin/`, `obj/`).

### 3.7 Packaging & distribution — transpiled output only

**Principle (per project requirement): the git repo is the F# source of truth; the
distributed artifacts (.vsix and the npm CLI package) contain only transpiled code.**
No F# sources, no generated `.fs.ts`, no source maps, no test files, no dotnet
build artifacts leave the repo except as shipped JS bundles.

The repo already enforces this convention for the current TS sources — `.vscodeignore`
excludes `src/**`, `**/*.ts`, `**/*.map`, `node_modules/**`, `out/**`, and the
config/docs files; `.npmignore` excludes `src/**` and the extension's artifacts. The
rewrite keeps the same shape and extends it to the new F#-specific artifacts.

**What ships, final state:**

| Artifact | Contents |
|---|---|
| `.vsix` (VS Code marketplace) | `dist/extension.js` (webpack bundle: all F# → TS → JS, Fable runtime inlined, web-tree-sitter JS bundled), `dist/web-tree-sitter.wasm`, `grammars/*.wasm` (4 grammars), `package.json`, `images/icon.png`, `README.md`, `LICENSE`, `CHANGELOG.md`, `action.yml` |
| npm package (CLI, `bin` + `action.yml`) | `dist/cli.js` (shebang banner), `grammars/*.wasm`, `package.json`, `action.yml`, `README.md`, `LICENSE`, `CHANGELOG.md` |

Distribution advantages this gives us:
- **Zero runtime npm dependencies** in either artifact — the Fable runtime is inlined
  into the generated TS (verified via the TS quick-start), and `web-tree-sitter`'s JS
  is bundled by webpack with its WASM as a copied asset. Install cost is the bundle +
  grammars, nothing else.
- Colocated generated TS (`src/**/*.fs.ts`) is automatically inside the `src/**`
  exclusion of both ignore files — another reason §3.1 keeps the F# under `src/`.

**Ignore-file updates (Phase 4):**
- `.vscodeignore` — add: `tests/**`, `**/*.fsproj`, `**/packages.lock.json`,
  `**/bin/**`, `**/obj/**`, `fable_modules/**`, and defensively `**/*.fs` /
  `**/*.fs.ts` (the existing `src/**`, `**/*.ts`, `**/*.map` already cover most).
- `.npmignore` — add the same F#-specific set, plus `**/*.map` (closes an existing
  gap: `dist/cli.js.map` is currently *not* excluded from the npm package) and
  `**/*.fs`.
- `.gitignore` — add: `*.fs.ts`, `fable_modules/`, `bin/`, `obj/` (generated
  intermediates and .NET build output; **NuGet lock files stay committed** — supply
  chain hygiene, matching Scriptorium's own `packages.lock.json` practice).

**Source-map policy:** production artifacts carry **no `.map` files** (status quo:
`.vscodeignore` has `**/*.map`; extended to `.npmignore` as above). F5 debugging
continues to work off the local dev build (`nosources-source-map`, never packaged).
Shipping maps with `sourcesContent` would leak the F# sources to every install —
explicitly out. If we later want stack-trace mapping in the shipped extension, that's
a conscious decision with an ADC, not a default.

**License banner:** the CLI bundle already gets a BannerPlugin shebang; add a
production banner to both bundles: `Energy State Analyzer — MIT — compiled from F#
source at <repo URL>` (cheap provenance for distributed transpiled code).

**Mechanical backstop** (per the repo's "backstops are mechanical" culture):
- New `just pack-check` recipe: `vsce package` to a temp file, list its contents
  (a .vsix is a zip), and **assert no `*.fs`, `*.fs.ts`, `*.map`, `tests/`, `obj/`,
  `fable_modules` entries**; same assertion via `npm pack --dry-run` for the CLI
  package.
- CI (Phase 4): a "Package contents" step that runs `pack-check` on every PR — a
  packaging regression (e.g. someone removes an ignore line) fails CI, not a user.

## 4. Phased plan (each phase ends with a green `just test`)

### Phase 0 — Toolchain spike (≈1 day)
De-risk the whole build/test chain with the smallest possible program before porting
anything.
- Add `fable 5.15.0` + `fantomas` to `.config/dotnet-tools.json`; `just setup` restores.
- Tiny F# project: one module with a record, a DU, a `task { }` block awaiting a JS
  promise (a `Task<'T>` binding), and a Scriptorium test that bridges it via
  `toAsync`/`Async.AwaitTask` and asserts on it — the Fable.Giraffe idiom, end to end.
- Prove: `dotnet fable --lang typescript` → `tsc` → `node` runs the Scriptorium suite
  with a correct exit code (pass and fail paths).
- Prove: webpack consumes the generated TS as an entry with `vscode` external (stub
  module) and the `web-tree-sitter` import resolves in the bundle.
- Nail the web-tree-sitter binding syntax (module/named-export access — promise awaiting
  is settled: task expressions are native `Promise<T>`) with a hello-world that parses
  one Python fixture and prints the root node type. **Done** — `tests/SpikeTests.fs` does
  exactly this; named-import bindings + `[<Emit("$0.method()")>]` work, property accessors
  use `[<Emit("$0.property")>]`.
- **Exit criteria**: `just spike` green (fable→node with a correct exit code on both the
  pass and fail paths); binding pattern documented in `src/Core/TreeSitter.fs`; generated
  git policy decided. **Status:** ✅ all met — `just spike` green (pass + fail), web-tree-sitter
  binding documented, webpack proof done, generated-file git policy applied, scratch cleaned.
  JS target means no `.fs.ts` — generated files are `<name>.js` / fable_modules `.fs.js`,
  all gitignored (`spike-js/`, `bin/`, `obj/`, `fable_modules/`). See "Phase 0 closed" above.

### Phase 1 — Core domain + full Scriptorium suite (the bulk; ≈1–2 weeks)
Port `src/core/*` + `src/languages/*` + `src/types.ts` to F# **with tests first**:
1. `Violation.fs`, `Context.fs`, `Position.fs` + their unit tests (Nib).
2. `TreeSitter.fs` binding finished (init/load/parse/node API).
3. `LanguageAdapter.fs` + all four language modules (Python, F#, TypeScript, Kotlin) —
   the record-of-functions shape, `Option` where the current code uses `null`.
4. Detectors, one at a time, **each gated by porting its integration suite**:
   `nesting → cyclomatic → cognitive → coherence (+namingCohesion/typeCohesion/
   classRelatedness) → magicNumber → magicString → parameterCount → inversion →
   matchOpportunity → logicalControlFlow → opaqueBoolean → primitiveObsession`.
5. `Esaignore.fs`, `Suppressions.fs`, `Analyze.fs` pipeline (+ the esaignore and
   suppressions suites; Hedgehog property tests for both if time allows).
6. Delete the corresponding `src/*.ts` as each lands (no dual-source window longer than
   one PR per detector batch).
- **Exit criteria**: all 17 integration suites green under Scriptorium/Node against the
  same fixtures; `just analyze src` (old CLI still in place) still passes on the
  remaining TS; ADC comments ported verbatim into F# module headers (they remain active
  constraints per AGENTS.md).

### Phase 2 — CLI (≈2–3 days)
- `Cli/Cli.fs` (entry, arg parsing ported 1:1 incl. the `VALUE_FLAGS` contract),
  `CliModes.fs`, `Scan.fs`, `Report*.fs`; Node APIs via `Fable.Node`.
- webpack `cli` entry retargets to the generated CLI TS; BannerPlugin shebang preserved
  (verify: ADC in `cli.ts` about the shebang — the bundle must still start with
  `#!/usr/bin/env node`).
- **Exit criteria**: `energy-state-cli <file>` JSON output byte-compatible with the old
  CLI (snapshot the legacy output for 3 fixtures first, diff after); scan mode,
  `--base-ref` diff mode, thresholds flags all green in a ported subset of
  `scan.test.ts`/`report.test.ts`; `just analyze src` now analyzes **our own F# code**
  (F# grammar) — triage findings per the AGENTS.md verdict process.

### Phase 3 — VS Code extension (≈3–5 days)
- `Extension/Vscode.fs` binding; `Grammar.fs` (parser init + per-language cache with the
  in-flight dedup — `Map<string, Task<LoadedLanguage>>`, i.e. native promises, so
  concurrent loads of the same grammar share one in-flight `Task`), `Decorations.fs`
  (incl. `createLightningIcon` data-URI and the heatmap), `Diagnostics.fs` (severity
  mapping, per-line grouping, tags), `Extension.fs` composition root (activation wiring,
  config-change handling — same behavior, same ADCs).
- Config reading (`energyStateAnalyzer.*` workspace settings) maps into the `Thresholds`
  record — single shared mapping with the CLI flags.
- Host-test strategy spike (§3.5): Scriptorium-under-@vscode/test-cli primary, mocha
  adapter fallback. F5 debug flow must still work (`.vscode/launch.json` unchanged).
- **Exit criteria**: F5 smoke pass (open a Python/F#/TS/Kotlin fixture, see decorations +
  Problems panel, edit and watch updates, `esa-ignore` directive honored);
  extension-host suite green; `just pack` produces a loadable `.vsix`.

### Phase 4 — Cleanup, tooling, CI (≈2 days)
- Remove: mocha devDeps (`@types/mocha`, `@vscode/test-cli` stays **only** if the
  host-test strategy needs it), ESLint/Prettier for F# (keep prettier for any residual
  JS config), old `src/*.ts`, `out/` tsc-for-tests step, `compile-tests`/`watch-tests`
  scripts.
- `.config/dotnet-tools.json`: `fable 5.15.0`, `fantomas`; Justfile rewrite (§3.6).
- `.esaignore`: add generated `*.fs.ts`; `.gitignore` per Phase 0 decision.
- **Packaging per §3.7**: extend `.vscodeignore`/`.npmignore` with the F#-specific
  exclusions (`tests/**`, `*.fsproj`, `bin/`, `obj/`, `fable_modules/`, `**/*.fs`,
  `**/*.fs.ts`, and `**/*.map` for npm); add the production license banner (webpack
  BannerPlugin); add the `just pack-check` recipe (assert the .vsix zip and
  `npm pack --dry-run` contain no F#/generated/map/test files) and a CI "Package
  contents" step that runs it.
- CI (`ci.yml`): add `actions/setup-dotnet` + `dotnet tool restore`; test step becomes
  `fable → tsc → node tests + host tests`; keep license check and **extend** it: verify
  Scriptorium/Fable NuGet licenses against the same allowlist (MIT expected; record the
  verdict in an ADC if any is non-MIT).
- Docs: `docs/architecture.md` module map rewritten for the F# layout (same coherence
  rules: ≤12 functions / ≤10 imports per F# module, now enforced by our own analyzer on
  F#); `AGENTS.md` build/test commands updated; `README.md` dev section (dotnet + Fable);
  detector docs unchanged (behavior identical).
- **Exit criteria**: `just install && just build && just test && just analyze &&
  just pack && just pack-check` green from a clean checkout; CI green (incl. the
  "Package contents" step proving the .vsix/npm contain no F# or generated files).

### Phase 5 — Parity & release (≈1–2 days)
- Golden-output regression: for every fixture in every language, old-CLI JSON == new-CLI
  JSON (automated diff script, run once pre-delete in Phase 2, re-run here post-cleanup).
- Pack `.vsix`, install in a clean VS Code, 10-minute real-world smoke across the four
  languages (incl. an F# file — dogfooding).
- **Distribution audit (final gate)**: `just pack-check` passes on the release build;
  manually inspect the .vsix + `npm pack` listing to confirm the manifest in §3.7
  (only transpiled JS + grammars + wasm + metadata — no `.fs`, `.fs.ts`, `.map`,
  tests, or dotnet artifacts).
- `just shipit --allow-branch main` to generate the release PR; Conventional Commit
  discipline per RELEASING.md (the rewrite commits themselves: `refactor: rewrite analyzer core in F# (Fable)` etc.).
- **Exit criteria**: release published; no open parity diffs; distribution audit clean.

**Total estimate: ~3–4 weeks** of focused work (Phases 1 and 3 dominate).

## 5. Risks and mitigations

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| 1 | **Fable 5 TS-target gaps** (edge F# constructs, module-state semantics) | Low-Med | Fable 5 stable + TS marked stable + task expressions verified as native `Promise<T>` (fable-compiler 2.4.0); Scriptorium itself is written against exactly this stack (strong evidence it works); Phase 0 spike still exercises records/DUs/module-state/promise-await *before* porting |
| 2 | **web-tree-sitter binding** is hand-written and unproven in Fable | Medium | Phase 0 spike; surface is tiny (~15 members); worst case, a small `Emit`-based bridge — Fable's documented escape hatch |
| 3 | **Quill under the VS Code extension host** (mocha environment) | Low-Med | Two ready strategies (§3.5); the suite is small (1 file today); if both fail, that one suite stays mocha-in-TS as a classified exception with an ADC |
| 4 | **Per-keystroke performance** regression (F#→TS overhead in extension host) | Low | The hot path is WASM parsing, unchanged; Fable 5 emits plain modern JS/TS; benchmark one large file (e.g. 3k-line TS) old vs new in Phase 3 before declaring done |
| 5 | **Dogfooding loop**: `just analyze src` must analyze F# well enough to be useful (F# fixtures are thinner than TS ones) | Medium | F# grammar + fixtures already exist; if F# detector coverage is too weak to dogfood, that's a *detector* PR, separate from this rewrite (AGENTS.md rule) |
| 6 | **Dual-source drift** during the rewrite | Medium | Phase 1 deletes each TS module in the same PR that its F# port lands; CI gates the whole thing; no "both green" windows across PRs |
| 7 | **NuGet license check** (npm license-checker won't see NuGet) | Low | Phase 4: manual/`dotnet` license audit recorded with an ADC; Scriptorium is fable-hub, expected MIT |
| 8 | Toolchain friction: dotnet SDK + node + npm on every dev machine and CI | Low | Repo already requires dotnet (ShipIt tool); CI gets one `setup-dotnet` step; document in README |
| 9 | **F# source (or generated `.fs.ts`) leaks into a distributed artifact** — e.g. a dropped ignore line, a new build-output dir not excluded, or a source map with `sourcesContent` | Low | The "ship transpiled only" policy is already enforced for TS via `.vscodeignore`/`.npmignore` and is extended for F# (§3.7); a `just pack-check` recipe + CI step mechanically asserts the .vsix zip and `npm pack` listing contain no `.fs`/`.fs.ts`/`.map`/test/dotnet files — a packaging regression fails CI, not a user |

## 6. What stays / what goes

**Stays unchanged**: `grammars/`, `images/`, `package.json` public contract
(main/bin/contributes/activationEvents/engines), `action.yml`, `RELEASING.md` + ShipIt,
`.esaignore` (grows), `.vscodeignore`/`.npmignore` (the "ship only transpiled output"
policy — extended, not replaced, see §3.7), ADC convention, coherence rules,
`docs/detectors/*`, `energy-state.md`.

**Goes away**: all hand-written `src/*.ts`, mocha test infrastructure for core tests,
ESLint/Prettier-over-F#, `out/` test-compile step, `@types/mocha` (maybe),
`compile-tests`/`watch-tests` scripts.

**Added**: F# sources under `src/`, `tests/` (Fable test project), two `.fsproj` files,
Fable + Fantomas dotnet tools, generated `*.fs.ts` (gitignored, never shipped), NuGet
lock files, the `just pack-check` recipe + CI "Package contents" step, and a production
license banner on the shipped bundles (§3.7).

## 7. Decisions made (with rationale) — push back on any of these

1. **Fable 5, JavaScript target** — the only current Fable line (stable since 2026-04).
   The plan originally chose the TS target (`--lang typescript`, marked stable) to keep
   `tsc` + webpack in the loop with zero new runtime npm deps. **During Phase 0 we switched
   to the JS target** because of a TS-target parse bug on `[<Emit("$0.property")>]`
   bindings (see Current status; fix prompt at `docs/fable-ts-target-task-bug-prompt.md`).
   The JS target emits plain JS directly — still no new runtime npm deps, and webpack stays
   in the loop (bundling the emitted `.js`); we just drop the standalone `tsc` step. If the
   TS bug is fixed upstream, re-evaluating the TS target is a one-line change back.
2. **Keep webpack** — it already solves the three hard packaging problems (vscode
   CommonJS external, `web-tree-sitter.wasm` asset copy, CLI shebang). Swapping bundlers
   is scope creep with no benefit.
3. **F# in `src/`, generated TS colocated** — smallest churn to every tool that already
   points at `src/` (`just analyze src`, `.esaignore`, `.vscodeignore`, coverage).
4. **Record-of-functions LanguageAdapter, DU-based violation model, Option where null** —
   "idiomatic F#" as required; also deletes the `as any` casts and the
   `?? DEFAULT_*` default-resolution noise.
5. **Pipeline as `Detector list` + final suppression pass** — the explicit requirement;
   preserves the analyze.ts ADC (single shared detector list for both entry points).
6. **Scriptorium for all F# tests incl. host tests** — the stated requirement; fallback
   adapter only if the spike disproves it (classified exception with ADC).
7. **No new detectors, no threshold changes** — rewrite-only scope; dogfooding findings
   on F# sources get the AGENTS.md verdict process (real/wrong-impl/wrong-threshold/
   legitimate-exception), each as its own PR.
8. **`Task` for async edges, plain sync for the core pipeline** — `Async` and `Task` are
   different in Fable 5: `task { }`/`Task<'T>` maps to native JS `Promise<T>`, while
   `async { }`/`Async<'T>` is Fable's separate event-loop implementation. So promise-based
   JS APIs (web-tree-sitter, the VS Code host) are bound as `Task` and the synchronous
   detector pipeline stays synchronous; the only bridge is `Async.AwaitTask`
   (`toAsync`) where Scriptorium's `Async<unit>` test bodies meet `Task` results.
   Pass `Task` values straight through — no `task { return! ... }` wrapper overhead
   (Fable.Giraffe `Core.fs:99`).
9. **Fable.Giraffe is the local reference implementation** — a working sibling repo on
   the same stack (Fable 5 + Scriptorium + `task { }`): `src/Core.fs` (Task pipeline
   composition), `src/js/HttpContext.fs` (promise bindings as `Task<'T>`),
   `test/js/Main.fs` (`main _ = runTests` entry), `test/shared/*` (`testAsync` +
   `toAsync` test idiom). Copy its patterns rather than inventing new ones.
10. **Distribute transpiled output only** — the git repo is the F# source of truth;
    the `.vsix` and npm CLI package ship only the compiled JS bundles + grammars +
    wasm + metadata. No F# sources, no generated `.fs.ts`, no source maps, no test
    files, no dotnet artifacts. Enforced by the existing `.vscodeignore`/`.npmignore`
    (extended for F#-specific files) and by a mechanical `just pack-check` + CI step
    that asserts the artifact file lists (§3.7). This keeps the F# source private to
    the repo and gives the shipped extension zero runtime npm dependencies.

## 8. First concrete steps (when approved)

1. Open PR `chore: add Fable 5 + Fantomas dotnet tools` (dotnet-tools.json, just
   `fable` recipe). **Done.**
2. Phase 0 spike branch: `src/Core/Hello.fs` + `tests/Main.fs` — prove the chain with the
   **JS target** (`dotnet fable --lang javascript`, add `{"type":"module"}` to the output
   `package.json` for node ESM, then `node <output>/Main.js`). No tsconfig tweak needed.
   **Done** — plus the webpack proof, fail-path exit code, `just spike` recipe, generated-file
   git policy, and scratch cleanup. Phase 0 is complete; see "Phase 0 closed".
3. Commit the Phase 0 F# layout + toolchain as a unit (`chore: ...`), then open Phase 1 PRs,
   one per detector batch, each deleting the old TS and flipping the test suite.
