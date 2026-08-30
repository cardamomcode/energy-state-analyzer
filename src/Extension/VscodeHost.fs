module Energy.Extension.VscodeHost

open Fable.Core

// The host-level portion of the small VS Code facade.
//
// decision: exposes only calls made by the extension so vscode remains an explicit, narrow
// Fable interop boundary rather than leaking dynamic objects into the analyzer core.

[<Import("window", "vscode")>]
let window: obj = nativeOnly

[<Import("workspace", "vscode")>]
let workspace: obj = nativeOnly

[<Import("languages", "vscode")>]
let languages: obj = nativeOnly

[<Import("commands", "vscode")>]
let commands: obj = nativeOnly

[<Emit("$0.activeTextEditor")>]
let activeTextEditor (hostWindow: obj) : obj = nativeOnly

[<Emit("$0.showInformationMessage($1)")>]
let showInformationMessage (hostWindow: obj) (message: string) : unit = nativeOnly

[<Emit("$0.showErrorMessage($1)")>]
let showErrorMessage (hostWindow: obj) (message: string) : unit = nativeOnly

[<Emit("$0.onDidChangeActiveTextEditor($1)")>]
let onDidChangeActiveTextEditor (hostWindow: obj) (handler: obj -> unit) : obj = nativeOnly

[<Emit("$0.registerCommand($1, $2)")>]
let registerCommand (hostCommands: obj) (command: string) (handler: unit -> unit) : obj = nativeOnly

[<Emit("$0.subscriptions.push($1)")>]
let addSubscription (context: obj) (disposable: obj) : unit = nativeOnly

[<Emit("$0.dispose()")>]
let dispose (disposable: obj) : unit = nativeOnly

[<Emit("$0.extensionPath")>]
let extensionPath (extensionContext: obj) : string = nativeOnly
