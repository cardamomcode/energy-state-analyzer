// # Workspace
//
// This narrow VS Code facade exposes resource-scoped settings and workspace/document events. Values are
// native host objects carried through Fable bindings; callers supply the appropriate live objects and
// handle host failures. ../Extension.fs owns activation and subscriptions, while pure presentation policy
// remains in ../DecorationModel.fs and ../DiagnosticModel.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Extension.Vscode.Workspace

open Fable.Core

/// Find the workspace folder owning a URI, returning the host's absent value for standalone documents.
[<Emit("$0.getWorkspaceFolder($1)")>]
let workspaceFolderFor (hostWorkspace: obj) (uri: obj) : obj = nativeOnly

/// Read the folder URI used to derive its filesystem root.
[<Emit("$0.uri")>]
let workspaceFolderUri (workspaceFolder: obj) : obj = nativeOnly

/// Read an unscoped configuration section; resource-aware callers use getConfigurationFor.
[<Emit("$0.getConfiguration($1)")>]
let getConfiguration (hostWorkspace: obj) (section: string) : obj = nativeOnly

/// Read settings in the scope of the document or workspace folder being analyzed.
[<Emit("$0.getConfiguration($1, $2)")>]
let getConfigurationFor (hostWorkspace: obj) (section: string) (resource: obj) : obj = nativeOnly

/// Expose the raw workspaceFolders array for root discovery before any document is open.
///
/// decision: read the raw workspaceFolders array so a project's .esaconfig.json can be discovered from
/// the root before any document is open; null when no folder exists, which callers treat as "no config".
[<Emit("$0.workspaceFolders")>]
let workspaceFolders (hostWorkspace: obj) : obj = nativeOnly

/// Read a key from a configuration object, using the supplied fallback when the host has no value.
[<Emit("$0.get($1, $2)")>]
let getConfigurationValue<'a> (configuration: obj) (key: string) (fallback: 'a) : 'a = nativeOnly

/// `get` returns contributed defaults too. Inspect separately so a setting only overrides
/// `.esaconfig.json` when a user has actually configured it in VS Code.
[<Emit("(() => { const setting = $0.inspect($1); return !!setting && ['globalValue', 'workspaceValue', 'workspaceFolderValue', 'globalLanguageValue', 'workspaceLanguageValue', 'workspaceFolderLanguageValue'].some(name => setting[name] !== undefined); })()")>]
let hasConfigurationOverride (configuration: obj) (key: string) : bool = nativeOnly

/// Register a text-change handler and return a disposable subscription.
[<Emit("$0.onDidChangeTextDocument($1)")>]
let onDidChangeTextDocument (hostWorkspace: obj) (handler: obj -> unit) : obj = nativeOnly

/// Register a settings-change handler and return a disposable subscription.
[<Emit("$0.onDidChangeConfiguration($1)")>]
let onDidChangeConfiguration (hostWorkspace: obj) (handler: obj -> unit) : obj = nativeOnly

/// Register a document-close handler and return a disposable subscription.
[<Emit("$0.onDidCloseTextDocument($1)")>]
let onDidCloseTextDocument (hostWorkspace: obj) (handler: obj -> unit) : obj = nativeOnly

/// Check whether a settings-change event affects a section before refreshing analysis or presentation.
[<Emit("$0.affectsConfiguration($1)")>]
let affectsConfiguration (event: obj) (section: string) : bool = nativeOnly
