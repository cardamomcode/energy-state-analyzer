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

// decision: passes the imported constructor explicitly because Fable substitutes Emit placeholders
// positionally; otherwise `$0` would become the range and compile to `new range(...)`.
[<Emit("new $0($1, $2, $3)")>]
let private constructDiagnostic (constructor: obj) (range: obj) (message: string) (severity: int) : obj = nativeOnly

let makeDiagnostic range message severity =
    constructDiagnostic diagnosticConstructor range message severity
