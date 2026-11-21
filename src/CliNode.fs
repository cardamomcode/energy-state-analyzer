// # CliNode
//
// These narrow Node bindings supply process arguments, command execution, console output, and exit
// behavior to the CLI. Calls retain native exception behavior unless a safe Core/NodeInterop wrapper is
// used. Bundle-relative asset lookup uses the emitted script location rather than the invoking working
// directory.
//
// Reading path: [Source guide](../docs/source-guide.md).

module Energy.CliNode

open Fable.Core
open Fable.Core.JS

// Narrow Node interop surface shared by CLI mode modules.
//
// decision: filesystem and path bindings come from Core.FsPath; this module owns only CLI process
// and output bindings so Node interop remains centralized without coupling Core to CLI behavior.

open Energy.Core.Paths

/// Execute a command synchronously with the supplied argument array and Node options; process failures
/// throw to the caller.
[<Import("execFileSync", "node:child_process")>]
let execFileSync (command: string) (arguments: string array) (options: obj) : string = nativeOnly

/// Test whether a Node stat result describes a regular file before accepting a CLI input.
[<Emit("$0.isFile()")>]
let isFile (stat: obj) : bool = nativeOnly

/// Read process arguments after the Node executable and CLI entry script.
[<Emit("process.argv.slice(2)")>]
let argv () : string array = nativeOnly

/// Directory containing the emitted CLI bundle, used to resolve packaged grammar assets.
[<Emit("__dirname")>]
let bundleDirectory: Path = nativeOnly

/// Terminate the Node process with the requested exit code; pending work is not awaited.
[<Emit("process.exit($0)")>]
let exit (code: int) : unit = nativeOnly

/// Write one diagnostic message to stderr through the Node console.
let error (message: string) : unit = console.error (message)

/// Write one report string to stdout through the Node console.
let output (message: string) : unit = console.log (message)

/// Serialize a report as indented JSON for the command's stdout contract.
[<Emit("JSON.stringify($0, null, 2)")>]
let stringify (value: obj) : string = nativeOnly
