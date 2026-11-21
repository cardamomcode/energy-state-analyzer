// # Document
//
// This narrow VS Code facade exposes document snapshots, locations, and decoration replacement. Values are
// native host objects carried through Fable bindings; callers supply the appropriate live objects and
// handle host failures. ../Extension.fs owns activation and subscriptions, while pure presentation policy
// remains in ../DecorationModel.fs and ../DiagnosticModel.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Extension.Vscode.Document

open Fable.Core

/// Read the document currently attached to an editor; callers supply a live editor object.
[<Emit("$0.document")>]
let editorDocument (editor: obj) : obj = nativeOnly

/// Replace one style's ranges on an editor; an empty array clears that style's previous decorations.
[<Emit("$0.setDecorations($1, $2)")>]
let setDecorations (editor: obj) (decoration: obj) (ranges: obj array) : unit = nativeOnly

/// Read the document's current in-memory text, including unsaved edits.
[<Emit("$0.getText()")>]
let documentText (document: obj) : string = nativeOnly

/// Read the document's filesystem name used for language and test-file policy.
[<Emit("$0.fileName")>]
let documentFileName (document: obj) : string = nativeOnly

/// Read the editor's language ID for registry lookup; this can differ from filename inference.
[<Emit("$0.languageId")>]
let documentLanguageId (document: obj) : string = nativeOnly

/// Read the document URI used as the diagnostics and workspace identity.
[<Emit("$0.uri")>]
let documentUri (document: obj) : obj = nativeOnly

/// Read the current number of document lines for heat-range bounds checks.
[<Emit("$0.lineCount")>]
let documentLineCount (document: obj) : int = nativeOnly

/// Read one zero-based line without its line terminator; invalid line indices can throw.
[<Emit("$0.lineAt($1).text")>]
let documentLineText (document: obj) (line: int) : string = nativeOnly

/// Read a URI's platform filesystem path for configuration and source access.
[<Emit("$0.fsPath")>]
let uriFsPath (uri: obj) : string = nativeOnly

/// Read the changed document from a text-change event before deciding whether active-editor analysis is
/// needed.
[<Emit("$0.document")>]
let textDocumentFromEvent (event: obj) : obj = nativeOnly
