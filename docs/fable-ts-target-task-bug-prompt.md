# Prompt — Fix Fable 5 TypeScript target: `task { }` emits invalid TS

Status: open. This is a self-contained, reproducible bug report + fix prompt for the
Fable compiler. It was filed from the energy-state-analyzer F# rewrite (which had to fall
back to `--lang javascript` because of this). Use it to drive a fix in Fable, then re-run
the energy-state-analyzer build with `--lang typescript` to confirm.

## TL;DR

On the **TypeScript** target (`--lang typescript`), Fable compiles a `task { }` computation
expression into a call chain on the `TaskBuilder` singleton that is **not valid
TypeScript**: it (a) types the builder as `any`, and (b) passes the **F#-side** generic
arity to `Delay`/`Bind`, which does not match the arity of the `TaskBuilder` methods in the
shipped `fable-library-ts`. The same F# source compiles to **clean, valid JS** on the
`--lang javascript` target. So this is a TS-target codegen gap, not an F# or library issue.

## Environment

- Fable `5.15.0` (dotnet tool); also reproduced against local checkout `5.15.0-16-g08abcd0e1`.
- `fable-library-ts.5.15.0` (the shipped TS library).
- dotnet SDK 10, Node 22, tsc 5.9.
- F# project: `Fable.Core 5.2.0`, one module, one `task { }` block.

## Minimal reproduction

`Hello.fs`:

```fsharp
module Hello
open System.Threading.Tasks
open Fable.Core

type Point = { X: int; Y: int }

[<Emit("Promise.resolve($0)")>]
let jsResolve (value: int) : Task<int> = nativeOnly

// A task block that awaits one promise.
let run (p: Point) : Task<int> =
    task {
        let! n = jsResolve p.X
        return n + p.Y
    }
```

`Hello.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <Compile Include="Hello.fs" />
    <PackageReference Include="Fable.Core" Version="5.2.0" />
  </ItemGroup>
</Project>
```

(Note: the F# SDK sets `EnableDefaultCompileItems=false`, so the `<Compile Include>` is
required or Fable will not see the file at all.)

Build and inspect:

```bash
dotnet fable Hello.fsproj --lang typescript --outDir out-ts
cat out-ts/Hello.ts            # invalid TS (see below)
npx tsc --noEmit --strict --target ES2022 --module ESNext --moduleResolution bundler \
    --allowImportingTsExtensions --skipLibCheck out-ts/Hello.ts   # errors

dotnet fable Hello.fsproj --lang javascript --outDir out-js
cat out-js/Hello.fs.js         # clean JS (see below)
```

## Actual output — TS target (INVALID)

`out-ts/Hello.ts`:

```ts
export function run(p: Point): any {
    const builder$0040: any = task();
    return builder$0040.Run<int32>(
        builder$0040.Delay<int32, int32>(
            <Data>(): ((arg0: FSharpRef<any>) => boolean) =>
                builder$0040.Bind<int32, int32, int32>(
                    Promise.resolve(p.X),
                    (_arg: int32) => builder$0040.Return<int32>((_arg + p.Y))
                )
        ));
}
```

`npx tsc --noEmit --strict` reports, for this file:

```
error TS2347: Untyped function calls may not accept type arguments.   (Run<int32>, Delay<...>, Bind<...> on an `any`)
```

Concretely the three problems:
1. `const builder$0040: any = task();` — the builder is typed `any` even though `task()`
   is declared to return `TaskBuilder`.
2. `builder$0040.Delay<int32, int32>(...)` — **2** type arguments.
3. `builder$0040.Bind<int32, int32, int32>(...)` — **3** type arguments.

## Expected output — valid TS matching the shipped library

The shipped `fable-library-ts.5.15.0/TaskBuilder.ts` declares:

```ts
export class TaskBuilder {
  public Bind<T, U>(computation: Promise<T>, binder: (x: T) => Promise<U>): Promise<U> { ... } // 2 type params
  public Delay<T>(generator: () => Promise<T>): () => Promise<T> { ... }                       // 1 type param
  public Return<T>(value?: T): Promise<T | undefined> { ... }                                  // 1 type param
  public Run<T>(computation: () => Promise<T>): Promise<T> { ... }                             // 1 type param
}
export function task(): TaskBuilder { return singleton; }
```

So the TS codegen should emit calls whose type-argument arities match these signatures, and
should type the builder as `TaskBuilder` (or `unknown`/inferred), not `any`. A correct
emission for the repro is, e.g.:

```ts
import { task } from "./fable_modules/fable-library-ts.5.15.0/TaskBuilder.ts";
export function run(p: Point): Promise<number> {
  const b = task();
  return b.Run<number>(b.Delay<number>(() =>
    b.Bind<number, number>(Promise.resolve(p.X), (n) => b.Return<number>(n + p.Y))
  ));
}
```

(Exact names aside — the point is: arities match the library, and the builder is not `any`.)

## Actual output — JS target (VALID, for contrast)

`out-js/Hello.fs.js`:

```js
import { task } from "./fable_modules/fable-library-js.5.15.0/TaskBuilder.js";
export function run(p) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() =>
        builder$0040.Bind(Promise.resolve(p.X), (_arg) =>
            builder$0040.Return((_arg + p.Y)))));
}
```

Clean, no type arguments, runs correctly under Node. This confirms the F#→Fable mapping of
`task { }` to the promise-based `TaskBuilder` is correct; only the **TS printer** is wrong.

## Root-cause hypothesis

The TS printer is emitting the **F#-side** generic arity of the task-builder methods
(`Delay`/`Bind` carry extra type parameters in the F# `TaskBuilder` signature) instead of
projecting onto the **TS library** signature. It also loses the `TaskBuilder` type and
falls back to `any`. The JS printer does not emit type arguments at all, so it is unaffected.

Places likely to hold the bug (Fable compiler, TS target code path):
- The printer/transform that lowers F# computation-expression steps (`Delay`, `Bind`,
  `Return`, `Run`) to Fable AST `Call`s with `TypeArgs` for the TS target.
- The TS `Type`/`TypeArgs` emission for `Call` nodes (where `TypeArgs` are printed as
  `<...>`).
- Where the builder expression's type is resolved (it should be `TaskBuilder`, not `any`).

Search terms in the Fable source: `TaskBuilder`, `taskBuilder`, `"task"`, `TypeArgs`,
`GetTypeArgs`, the TS-specific `Call`/`Type` printers, and the `System.Threading.Tasks`
task-builder lowering.

## Suggested fix

1. For the TS target, lower `task { }` steps to calls whose `TypeArgs` match the
   `fable-library-ts` `TaskBuilder` method arities (`Run<T>`, `Delay<T>`, `Bind<T,U>`,
   `Return<T>`, `ReturnFrom<T>`, `Combine<T>`, `While`, `For<T>`, `Using<T,U>`,
   `TryWith<T>`, `TryFinally<T>`), or emit them with **no** type arguments (letting TS infer)
   — either is valid; matching the library is cleanest.
2. Type the builder value as `TaskBuilder` (import from `fable-library-ts/TaskBuilder`)
   rather than `any`.
3. Add/extend a TS-target test: a `task { }` block that `let!`-awaits a promise-bound value,
   asserting the emitted TS (a) type-checks under `tsc --strict --noEmit`, and (b) runs and
   resolves under Node. (The JS target likely already has such a test; mirror it for TS.)

## Acceptance (for the energy-state-analyzer rewrite)

After the fix, this must work with no source changes and no post-processing:

```bash
dotnet fable tests/EnergyState.Tests.fsproj --lang typescript --outDir out
npx tsc --noEmit --strict ... out/Main.ts            # 0 errors
# webpack(ts-loader) bundle out/Main.ts -> dist/tests.js
node dist/tests.js                                    # Scriptorium suite, exit 0 on pass / 1 on fail
```

Where `tests/` includes Scriptorium (`Scriptorium.Quill`, `Scriptorium.Nib`) async test
bodies that bridge `Task` → `Async` via `Async.AwaitTask`.

## Secondary (separate) TS-target issue observed

While validating, a second, smaller TS-target inconsistency was seen: for **source
packages in `fable_modules`** (e.g. Scriptorium), the emitted files are `*.fs.ts` but the
generated import specifiers are `*.fs.js` (the printer hardcodes the `JavaScript` default
extension for fable_modules import paths, see `changeExtensionButUseDefaultExtensionInFableModules
JavaScript ...` in `Fable.Cli/Pipeline.fs`). The JS target is self-consistent here. Worth
fixing alongside so `--lang typescript` import specifiers match the emitted extensions.
(workaround used in the rewrite: rewrite `*.fs.js"` → `*.fs.ts"` in generated files.)
