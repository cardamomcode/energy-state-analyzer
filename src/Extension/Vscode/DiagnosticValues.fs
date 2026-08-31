module Energy.Extension.Vscode.DiagnosticValues

open Fable.Core

[<Import("DiagnosticSeverity", "vscode")>]
let private diagnosticSeverity: obj = nativeOnly

[<Import("DiagnosticTag", "vscode")>]
let private diagnosticTag: obj = nativeOnly

[<Emit("$0.Error")>]
let private errorSeverity (values: obj) : int = nativeOnly

[<Emit("$0.Warning")>]
let private warningSeverity (values: obj) : int = nativeOnly

[<Emit("$0.Information")>]
let private informationSeverity (values: obj) : int = nativeOnly

[<Emit("$0.Unnecessary")>]
let private unnecessaryTag (values: obj) : int = nativeOnly

[<Emit("$0.Deprecated")>]
let private deprecatedTag (values: obj) : int = nativeOnly

[<Emit("$0.source = $1")>]
let setDiagnosticSource (diagnostic: obj) (source: string) : unit = nativeOnly

[<Emit("$0.code = $1")>]
let setDiagnosticCode (diagnostic: obj) (code: string) : unit = nativeOnly

[<Emit("$0.tags = $1")>]
let setDiagnosticTags (diagnostic: obj) (tags: int array) : unit = nativeOnly

let error = errorSeverity diagnosticSeverity
let warning = warningSeverity diagnosticSeverity
let information = informationSeverity diagnosticSeverity
let unnecessary = unnecessaryTag diagnosticTag
let deprecated = deprecatedTag diagnosticTag
