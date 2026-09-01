module Energy.CliNode

open Fable.Core
open Fable.Core.JS
open Fable.Core.JsInterop

// Narrow Node interop surface shared by CLI mode modules.
//
// decision: path and encoding arguments are Core.Paths newtypes (erased to their backing strings)
// so callers can no longer transpose a path with another string at the binding boundary.

open Energy.Core.Paths

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: Path) (encoding: Encoding) : string = nativeOnly

[<Import("existsSync", "node:fs")>]
let existsSync (path: Path) : bool = nativeOnly

[<Import("statSync", "node:fs")>]
let statSync (path: Path) : obj = nativeOnly

[<Import("relative", "node:path")>]
let relativePath (fromPath: Path) (toPath: Path) : string = nativeOnly

[<Import("resolve", "node:path")>]
let resolvePath (path: Path) : Path = nativeOnly

[<Import("join", "node:path")>]
let joinPath (left: Path) (right: Path) : Path = nativeOnly

[<Import("execFileSync", "node:child_process")>]
let execFileSync (command: string) (arguments: string array) (options: obj) : string = nativeOnly

[<Emit("$0.isFile()")>]
let isFile (stat: obj) : bool = nativeOnly

[<Emit("process.argv.slice(2)")>]
let argv () : string array = nativeOnly

[<Emit("process.cwd()")>]
let cwd () : string = nativeOnly

[<Emit("__dirname")>]
let bundleDirectory: Path = nativeOnly

[<Emit("process.exit($0)")>]
let exit (code: int) : unit = nativeOnly

let error (message: string) : unit = console.error (message)

let output (message: string) : unit = console.log (message)

[<Emit("JSON.stringify($0, null, 2)")>]
let stringify (value: obj) : string = nativeOnly
