// # DiagnosticValues
//
// This narrow VS Code facade exposes host enumeration values and diagnostic metadata mutation. Values are
// native host objects carried through Fable bindings; callers supply the appropriate live objects and
// handle host failures. ../Extension.fs owns activation and subscriptions, while pure presentation policy
// remains in ../DecorationModel.fs and ../DiagnosticModel.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Extension.Vscode.DiagnosticValues

open Fable.Core

/// Host enumeration supplying runtime severity values, avoiding hard-coded numeric assumptions.
[<Import("DiagnosticSeverity", "vscode")>]
let private diagnosticSeverity: obj = nativeOnly

/// Host enumeration supplying runtime tag values.
[<Import("DiagnosticTag", "vscode")>]
let private diagnosticTag: obj = nativeOnly

/// Read the error value from a host severity enumeration.
[<Emit("$0.Error")>]
let private errorSeverity (values: obj) : int = nativeOnly

/// Read the warning value from a host severity enumeration.
[<Emit("$0.Warning")>]
let private warningSeverity (values: obj) : int = nativeOnly

/// Read the information value from a host severity enumeration.
[<Emit("$0.Information")>]
let private informationSeverity (values: obj) : int = nativeOnly

/// Read the unnecessary value from a host tag enumeration.
[<Emit("$0.Unnecessary")>]
let private unnecessaryTag (values: obj) : int = nativeOnly

/// Read the deprecated value from a host tag enumeration.
[<Emit("$0.Deprecated")>]
let private deprecatedTag (values: obj) : int = nativeOnly

/// Assign the displayed analyzer source on a newly constructed host diagnostic.
[<Emit("$0.source = $1")>]
let setDiagnosticSource (diagnostic: obj) (source: string) : unit = nativeOnly

/// Assign stable rule-code text on a host diagnostic before publishing it.
[<Emit("$0.code = $1")>]
let setDiagnosticCode (diagnostic: obj) (code: string) : unit = nativeOnly

/// Replace a host diagnostic's presentation tags before publishing it.
[<Emit("$0.tags = $1")>]
let setDiagnosticTags (diagnostic: obj) (tags: int array) : unit = nativeOnly

/// Runtime host severity used for high analyzer findings.
let error = errorSeverity diagnosticSeverity
/// Runtime host severity used for medium analyzer findings.
let warning = warningSeverity diagnosticSeverity
/// Runtime host severity used for low analyzer findings.
let information = informationSeverity diagnosticSeverity
/// Runtime host tag used for nesting findings.
let unnecessary = unnecessaryTag diagnosticTag
/// Runtime host tag used for complexity findings.
let deprecated = deprecatedTag diagnosticTag
