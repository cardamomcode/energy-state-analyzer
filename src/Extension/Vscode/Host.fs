// # Host
//
// This narrow VS Code facade exposes lifecycle, commands, notifications, and resource disposal. Values are
// native host objects carried through Fable bindings; callers supply the appropriate live objects and
// handle host failures. ../Extension.fs owns activation and subscriptions, while pure presentation policy
// remains in ../DecorationModel.fs and ../DiagnosticModel.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Extension.Vscode.Host

open System.Threading.Tasks

open Fable.Core

/// The host-level portion of the small VS Code facade.
///
/// decision: exposes only calls made by the extension so vscode remains an explicit, narrow
/// Fable interop boundary rather than leaking dynamic objects into the analyzer core.

[<Import("window", "vscode")>]
let window: obj = nativeOnly

/// VS Code workspace surface for resource-scoped settings and document events.
[<Import("workspace", "vscode")>]
let workspace: obj = nativeOnly

/// VS Code language surface used to allocate the diagnostics collection.
[<Import("languages", "vscode")>]
let languages: obj = nativeOnly

/// VS Code command registry used for manual analysis and SARIF export.
[<Import("commands", "vscode")>]
let commands: obj = nativeOnly

/// Read the current editor, which may be absent; callers check absence before accessing its document.
[<Emit("$0.activeTextEditor")>]
let activeTextEditor (hostWindow: obj) : obj = nativeOnly

/// Request an informational notification; this wrapper does not await its dismissal.
[<Emit("$0.showInformationMessage($1)")>]
let showInformationMessage (hostWindow: obj) (message: string) : unit = nativeOnly

/// Request an error notification; this wrapper does not await its dismissal.
[<Emit("$0.showErrorMessage($1)")>]
let showErrorMessage (hostWindow: obj) (message: string) : unit = nativeOnly

/// Subscribe to active-editor changes and return the disposable registration owned by the extension
/// context.
[<Emit("$0.onDidChangeActiveTextEditor($1)")>]
let onDidChangeActiveTextEditor (hostWindow: obj) (handler: obj -> unit) : obj = nativeOnly

/// Register a synchronous command callback and return its disposable registration.
[<Emit("$0.registerCommand($1, $2)")>]
let registerCommand (hostCommands: obj) (command: string) (handler: unit -> unit) : obj = nativeOnly

/// Register a task-returning command so the host can observe completion or rejection, and return its
/// disposable registration.
[<Emit("$0.registerCommand($1, $2)")>]
let registerAsyncCommand (hostCommands: obj) (command: string) (handler: unit -> Task<unit>) : obj = nativeOnly

/// Ask VS Code to open a URI and await the resulting editor; host errors reject the task.
[<Emit("$0.showTextDocument($1)")>]
let showTextDocument (hostWindow: obj) (uri: obj) : Task<obj> = nativeOnly

/// Transfer a disposable registration to the extension context for host-managed cleanup.
[<Emit("$0.subscriptions.push($1)")>]
let addSubscription (context: obj) (disposable: obj) : unit = nativeOnly

/// Release a host resource or registration through its dispose method.
[<Emit("$0.dispose()")>]
let dispose (disposable: obj) : unit = nativeOnly

/// Read the installed extension directory used as the base for packaged grammar paths.
[<Emit("$0.extensionPath")>]
let extensionPath (extensionContext: obj) : string = nativeOnly
