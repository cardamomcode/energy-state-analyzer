module Energy.Extension.Vscode.Document

open Fable.Core

[<Emit("$0.document")>]
let editorDocument (editor: obj) : obj = nativeOnly

[<Emit("$0.setDecorations($1, $2)")>]
let setDecorations (editor: obj) (decoration: obj) (ranges: obj array) : unit = nativeOnly

[<Emit("$0.getText()")>]
let documentText (document: obj) : string = nativeOnly

[<Emit("$0.fileName")>]
let documentFileName (document: obj) : string = nativeOnly

[<Emit("$0.languageId")>]
let documentLanguageId (document: obj) : string = nativeOnly

[<Emit("$0.uri")>]
let documentUri (document: obj) : obj = nativeOnly

[<Emit("$0.lineCount")>]
let documentLineCount (document: obj) : int = nativeOnly

[<Emit("$0.lineAt($1).text")>]
let documentLineText (document: obj) (line: int) : string = nativeOnly

[<Emit("$0.fsPath")>]
let uriFsPath (uri: obj) : string = nativeOnly

[<Emit("$0.document")>]
let textDocumentFromEvent (event: obj) : obj = nativeOnly
