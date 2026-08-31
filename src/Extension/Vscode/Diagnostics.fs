module Energy.Extension.Vscode.Diagnostics

open Fable.Core

[<Import("Diagnostic", "vscode")>]
let private diagnosticConstructor: obj = nativeOnly

[<Emit("$0.createDiagnosticCollection($1)")>]
let createDiagnosticCollection (hostLanguages: obj) (name: string) : obj = nativeOnly

[<Emit("$0.set($1, $2)")>]
let setDiagnostics (collection: obj) (uri: obj) (diagnostics: obj array) : unit = nativeOnly

[<Emit("$0.delete($1)")>]
let deleteDiagnostics (collection: obj) (uri: obj) : unit = nativeOnly

[<Emit("$0.clear()")>]
let clearDiagnostics (collection: obj) : unit = nativeOnly

[<Emit("new $0($1, $2, $3)")>]
let makeDiagnostic (range: obj) (message: string) (severity: int) : obj = nativeOnly
