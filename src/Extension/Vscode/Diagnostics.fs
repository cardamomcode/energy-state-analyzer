// # Diagnostics
//
// This narrow VS Code facade exposes diagnostic construction and collection replacement. Values are native
// host objects carried through Fable bindings; callers supply the appropriate live objects and handle host
// failures. ../Extension.fs owns activation and subscriptions, while pure presentation policy remains in
// ../DecorationModel.fs and ../DiagnosticModel.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Extension.Vscode.Diagnostics

open Fable.Core

/// Imported host Diagnostic constructor passed explicitly to the emitted allocation binding.
[<Import("Diagnostic", "vscode")>]
let private diagnosticConstructor: obj = nativeOnly

/// Allocate a named collection; the extension owns its lifetime and registers it for cleanup.
[<Emit("$0.createDiagnosticCollection($1)")>]
let createDiagnosticCollection (hostLanguages: obj) (name: string) : obj = nativeOnly

/// Replace diagnostics for one URI; an empty array clears that document's findings.
[<Emit("$0.set($1, $2)")>]
let setDiagnostics (collection: obj) (uri: obj) (diagnostics: obj array) : unit = nativeOnly

/// Remove a document URI from the collection when it closes or becomes ignored.
[<Emit("$0.delete($1)")>]
let deleteDiagnostics (collection: obj) (uri: obj) : unit = nativeOnly

/// Remove all documents' diagnostics from this collection without disposing it.
[<Emit("$0.clear()")>]
let clearDiagnostics (collection: obj) : unit = nativeOnly

/// Build a VS Code Diagnostic object from a range, message, and severity.
///
/// decision: passes the imported constructor explicitly because Fable substitutes Emit placeholders
/// positionally; otherwise `$0` would become the range and compile to `new range(...)`.
[<Emit("new $0($1, $2, $3)")>]
let private constructDiagnostic (constructor: obj) (range: obj) (message: string) (severity: int) : obj = nativeOnly

/// Create a host diagnostic with the supplied range, message, and host severity value.
let makeDiagnostic range message severity =
    constructDiagnostic diagnosticConstructor range message severity
