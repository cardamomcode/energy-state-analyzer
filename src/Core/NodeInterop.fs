module Energy.Core.NodeInterop

open Fable.Core
open Fable.Core.JsInterop
open Energy.Core.Paths

// decision: the safe (never-throwing) counterparts to the Node bindings in CliNode.fs. Callers that
// must not abort the run use these; the unsafe, throwing bindings stay in CliNode.fs for callers that
// want exceptions — this is the safe/unsafe split the interop surface is organised around. Keeping the
// safe variants here (not at each call site) means every boundary that must not throw goes through one
// reviewed place, and none of them write `try/with` themselves.

// decision: the safe wrappers here must call the throwing Node bindings, but Core cannot open the CLI
// module (that would invert the dependency), so these erased re-bindings are declared locally. Path/
// Encoding newtypes erase to their backing strings at runtime — identical signatures to CliNode.fs's.
[<Import("readFileSync", "node:fs")>]
let private unsafeReadFileSync (path: Path) (encoding: Encoding) : string = nativeOnly

[<Import("execFileSync", "node:child_process")>]
let private unsafeExecFileSync (command: string) (arguments: string array) (options: obj) : string = nativeOnly

// decision: JSON.parse has no F#-side signature to coerce into, so this tiny binding mirrors the one
// Config.fs used — erased to JSON.parse — before this module turns failures into data.
let private isNullOrUndefined (value: obj) : bool = value = null

[<Emit("JSON.parse($0)")>]
let private jsonParse (text: string) : obj = nativeOnly

// decision: attempt runs a possibly-throwing thunk and turns any exception into an Error message. It is
// the only try/with on this surface, and its whole body is the guarded operation plus its handler —
// signature only outside the error region — so the error-shadowing detector skips it as a thin wrapper
// with no unguarded business logic to shadow. Callers stay free of try/with and never abort the run.
let private attempt<'T> (fn: unit -> 'T) : Result<'T, string> =
    try
        Ok(fn ())
    with exn ->
        Error(string<exn> exn)

let readFileSyncSafe (path: Path) (encoding: Encoding) : Result<string, string> =
    attempt (fun () -> unsafeReadFileSync path encoding)

let execFileSyncSafe (command: string) (arguments: string array) (options: obj) : Result<string, string> =
    attempt (fun () -> unsafeExecFileSync command arguments options)

let jsonParseSafe (text: string) : obj option =
    // decision: JSON.parse is an [<Emit>] binding that Fable only preserves at a direct call site, so it
    // cannot ride through the generic `attempt` thunk — its single throw is wrapped here instead. The body
    // is signature-only outside the error region, so the error-shadowing detector treats this as a thin
    // wrapper with no unguarded logic to shadow; callers stay free of try/with and never abort the run.
    try
        let parsed = jsonParse text

        if isNullOrUndefined parsed then None else Some parsed
    with _ ->
        None
