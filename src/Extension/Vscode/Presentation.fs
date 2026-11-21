// # Presentation
//
// This narrow VS Code facade exposes typed ranges, URIs, and decoration handles. Values are native host
// objects carried through Fable bindings; callers supply the appropriate live objects and handle host
// failures. ../Extension.fs owns activation and subscriptions, while pure presentation policy remains in
// ../DecorationModel.fs and ../DiagnosticModel.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Extension.Vscode.Presentation

open Fable.Core

/// Imported VS Code Uri factory for encoded and filesystem locations.
[<Import("Uri", "vscode")>]
let private uriConstructor: obj = nativeOnly

/// Imported VS Code Range constructor, passed explicitly to the emitted allocation binding.
[<Import("Range", "vscode")>]
let private rangeConstructor: obj = nativeOnly

/// A 0-based editor row / column as VS Code reports it.
///
/// decision: erased to their backing ints (the Core.TreeSitter.NodeType pattern) so the four
/// adjacent int arguments of `new Range(...)` keep their prior runtime shape while F# can no
/// longer transpose a row with a column.
/// invariant: every `Line`/`Column` value has exactly its wrapped int as its JavaScript
/// representation.
[<Erase>]
type Line = Line of int

/// Zero-based editor column, erased to an integer while remaining distinct from Line in F# calls.
[<Erase>]
type Column = Column of int

/// Parse URI text through the supplied host factory; malformed input may throw.
[<Emit("$0.parse($1)")>]
let private parseUri (constructor: obj) (value: string) : obj = nativeOnly

/// Create a host URI from encoded URI text, including SVG data URIs.
let uriFromString value = parseUri uriConstructor value

/// Create a file URI through the supplied host factory using a platform filesystem path.
[<Emit("$0.file($1)")>]
let private fileUri (constructor: obj) (path: string) : obj = nativeOnly

/// Create a host file URI from a filesystem path rather than treating the path as encoded URI text.
let uriFromFilePath path = fileUri uriConstructor path

/// Allocate a host range from typed zero-based start and exclusive-end coordinates using the supplied
/// constructor.
[<Emit("new $0($1, $2, $3, $4)")>]
let private constructRange
    (constructor: obj)
    (startLine: Line)
    (startColumn: Column)
    (endLine: Line)
    (endColumn: Column)
    : obj =
    nativeOnly

/// Create a VS Code range from distinct line and column values for decoration or diagnostic presentation.
let makeRange (startLine: Line) (startColumn: Column) (endLine: Line) (endColumn: Column) =
    constructRange rangeConstructor startLine startColumn endLine endColumn

/// Allocate a decoration style from host options; callers must dispose the returned handle.
[<Emit("$0.createTextEditorDecorationType($1)")>]
let createTextEditorDecorationType (hostWindow: obj) (options: obj) : obj = nativeOnly
