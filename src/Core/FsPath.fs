module Energy.Core.FsPath

// esa-ignore-file: primitive-obsession
// A binding facade is inherently string/obj plumbing; the semantics live in how callers use these,
// not in the declarations themselves.

// decision: single internal facade for the fs/path Node bindings shared by the scanning and
// esaignore-loading modules. Fable's `[<Import>]` is the idiomatic way to bind a Node module, so
// this keeps every `node:fs`/`node:path` reference in one place instead of duplicating it per file.

open Fable.Core

[<Import("existsSync", "node:fs")>]
let existsSync (path: string) : bool = nativeOnly

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: string) (_encoding: string) : string = nativeOnly

[<Import("readdirSync", "node:fs")>]
let readdirSync: obj = nativeOnly

[<Import("statSync", "node:fs")>]
let statSync (path: string) : obj = nativeOnly

[<Import("basename", "node:path")>]
let basename (path: string) : string = nativeOnly

[<Import("join", "node:path")>]
let joinPath (left: string) (right: string) : string = nativeOnly

[<Import("relative", "node:path")>]
let relativePath (fromPath: string) (toPath: string) : string = nativeOnly

[<Import("resolve", "node:path")>]
let resolvePath (path: string) : string = nativeOnly

[<Import("sep", "node:path")>]
let pathSeparator: string = nativeOnly
