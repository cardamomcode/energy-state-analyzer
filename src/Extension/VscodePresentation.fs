module Energy.Extension.VscodePresentation

open Fable.Core

[<Import("Uri", "vscode")>]
let private uriConstructor: obj = nativeOnly

[<Import("Range", "vscode")>]
let private rangeConstructor: obj = nativeOnly

[<Emit("$0.parse($1)")>]
let private parseUri (constructor: obj) (value: string) : obj = nativeOnly

let uriFromString value = parseUri uriConstructor value

[<Emit("new $0($1, $2, $3, $4)")>]
let private constructRange
    (constructor: obj)
    (startLine: int)
    (startColumn: int)
    (endLine: int)
    (endColumn: int)
    : obj =
    nativeOnly

let makeRange startLine startColumn endLine endColumn =
    constructRange rangeConstructor startLine startColumn endLine endColumn

[<Emit("$0.createTextEditorDecorationType($1)")>]
let createTextEditorDecorationType (hostWindow: obj) (options: obj) : obj = nativeOnly
