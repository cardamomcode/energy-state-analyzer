module Energy.CliNode

open Fable.Core
open Fable.Core.JS

// Narrow Node interop surface shared by CLI mode modules.
//
// decision: filesystem and path bindings come from Core.FsPath; this module owns only CLI process
// and output bindings so Node interop remains centralized without coupling Core to CLI behavior.

open Energy.Core.Paths

[<Import("execFileSync", "node:child_process")>]
let execFileSync (command: string) (arguments: string array) (options: obj) : string = nativeOnly

[<Emit("$0.isFile()")>]
let isFile (stat: obj) : bool = nativeOnly

[<Emit("process.argv.slice(2)")>]
let argv () : string array = nativeOnly

[<Emit("__dirname")>]
let bundleDirectory: Path = nativeOnly

[<Emit("process.exit($0)")>]
let exit (code: int) : unit = nativeOnly

let error (message: string) : unit = console.error (message)

let output (message: string) : unit = console.log (message)

[<Emit("JSON.stringify($0, null, 2)")>]
let stringify (value: obj) : string = nativeOnly
