module Energy.Core.NodeInterop

open Fable.Core
open Fable.Core.JsInterop
open Energy.Core.FsPath
open Energy.Core.Paths

// decision: the safe (never-throwing) counterparts to the Node bindings in FsPath/CliNode. Callers
// that must not abort the run use these wrappers, keeping error conversion in one reviewed place.

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
    attempt (fun () -> readFileSync path encoding)

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
