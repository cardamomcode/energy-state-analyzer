module Energy.Extension.Vscode.Workspace

open Fable.Core

[<Emit("$0.getWorkspaceFolder($1)")>]
let workspaceFolderFor (hostWorkspace: obj) (uri: obj) : obj = nativeOnly

[<Emit("$0.uri")>]
let workspaceFolderUri (workspaceFolder: obj) : obj = nativeOnly

[<Emit("$0.getConfiguration($1)")>]
let getConfiguration (hostWorkspace: obj) (section: string) : obj = nativeOnly

// decision: read the raw workspaceFolders array so a project's .esaconfig.json can be discovered from
// the root before any document is open; null when no folder exists, which callers treat as "no config".
[<Emit("$0.workspaceFolders")>]
let workspaceFolders (hostWorkspace: obj) : obj = nativeOnly

[<Emit("$0.get($1, $2)")>]
let getConfigurationValue<'a> (configuration: obj) (key: string) (fallback: 'a) : 'a = nativeOnly

[<Emit("$0.onDidChangeTextDocument($1)")>]
let onDidChangeTextDocument (hostWorkspace: obj) (handler: obj -> unit) : obj = nativeOnly

[<Emit("$0.onDidChangeConfiguration($1)")>]
let onDidChangeConfiguration (hostWorkspace: obj) (handler: obj -> unit) : obj = nativeOnly

[<Emit("$0.onDidCloseTextDocument($1)")>]
let onDidCloseTextDocument (hostWorkspace: obj) (handler: obj -> unit) : obj = nativeOnly

[<Emit("$0.affectsConfiguration($1)")>]
let affectsConfiguration (event: obj) (section: string) : bool = nativeOnly
