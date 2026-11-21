// # Identity
//
// This narrow VS Code facade exposes strict JavaScript object identity for stale-work checks. Values are
// native host objects carried through Fable bindings; callers supply the appropriate live objects and
// handle host failures. ../Extension.fs owns activation and subscriptions, while pure presentation policy
// remains in ../DecorationModel.fs and ../DiagnosticModel.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Extension.Vscode.Identity

open Fable.Core

/// Compare host values using JavaScript strict identity, avoiding structural comparison of editor and
/// lifecycle objects.
[<Emit("$0 === $1")>]
let sameObject (left: obj) (right: obj) : bool = nativeOnly
