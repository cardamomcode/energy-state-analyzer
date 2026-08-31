module Energy.Extension.Vscode.Identity

open Fable.Core

[<Emit("$0 === $1")>]
let sameObject (left: obj) (right: obj) : bool = nativeOnly
