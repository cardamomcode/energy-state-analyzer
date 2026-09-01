module Energy.Extension.Vscode.Presentation

open Fable.Core

[<Import("Uri", "vscode")>]
let private uriConstructor: obj = nativeOnly

[<Import("Range", "vscode")>]
let private rangeConstructor: obj = nativeOnly

// A 0-based editor row / column as VS Code reports it.
//
// decision: erased to their backing ints (the Core.TreeSitter.NodeType pattern) so the four
// adjacent int arguments of `new Range(...)` keep their prior runtime shape while F# can no
// longer transpose a row with a column.
// invariant: every `Line`/`Column` value has exactly its wrapped int as its JavaScript
// representation.
[<Erase>]
type Line = Line of int

[<Erase>]
type Column = Column of int

[<Emit("$0.parse($1)")>]
let private parseUri (constructor: obj) (value: string) : obj = nativeOnly

let uriFromString value = parseUri uriConstructor value

[<Emit("new $0($1, $2, $3, $4)")>]
let private constructRange
    (constructor: obj)
    (startLine: Line)
    (startColumn: Column)
    (endLine: Line)
    (endColumn: Column)
    : obj =
    nativeOnly

let makeRange (startLine: Line) (startColumn: Column) (endLine: Line) (endColumn: Column) =
    constructRange rangeConstructor startLine startColumn endLine endColumn

[<Emit("$0.createTextEditorDecorationType($1)")>]
let createTextEditorDecorationType (hostWindow: obj) (options: obj) : obj = nativeOnly
