module Energy.Core.FsPath

// decision: single internal facade for the fs/path Node bindings shared by the scanning and
// esaignore-loading modules. Fable's `[<Import>]` is the idiomatic way to bind a Node module, so
// this keeps every `node:fs`/`node:path` reference in one place instead of duplicating it per file.
// decision: path and encoding arguments are Core.Paths newtypes (erased to their backing strings)
// so callers can no longer transpose a path with another string at the binding boundary.

open Fable.Core
open Energy.Core.Paths

[<Import("existsSync", "node:fs")>]
let existsSync (path: Path) : bool = nativeOnly

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: Path) (encoding: Encoding) : string = nativeOnly

[<Import("readdirSync", "node:fs")>]
let readdirSync: obj = nativeOnly

[<Import("statSync", "node:fs")>]
let statSync (path: Path) : obj = nativeOnly

[<Import("basename", "node:path")>]
let basename (path: Path) : string = nativeOnly

// decision: path-producing bindings return Path so results flow straight into other bindings
// (existsSync/readFileSync/joinPath) without a string round-trip at every call site.
[<Import("join", "node:path")>]
let joinPath (left: Path) (right: Path) : Path = nativeOnly

[<Import("relative", "node:path")>]
let relativePath (fromPath: Path) (toPath: Path) : string = nativeOnly

[<Import("resolve", "node:path")>]
let resolvePath (path: Path) : Path = nativeOnly

[<Import("sep", "node:path")>]
let pathSeparator: string = nativeOnly
