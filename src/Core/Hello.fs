module Energy.Core.Hello

open System.Threading.Tasks

open Fable.Core

// decision: Phase 0 spike module — proves the Fable 5 TypeScript target, a record, a
// discriminated union, and a `task { }` block awaiting a native JS promise (bound as
// Task<'T>). Deleted in Phase 1 once the real Core modules replace it (or kept as a
// canary if it stays useful).
type Color = Red | Green | Blue

type Point =
    { X: int
      Y: int
      Color: Color option }

// A promise-based JS API, bound as Task<'T> (native Promise). Fable maps Task<'T> to a
// native Promise on the js/ts targets, so `let!` inside a task { } block is a native await.
[<Emit("Promise.resolve($0)")>]
let jsResolve (value: int) : Task<int> = nativeOnly

let greet (p: Point) : string =
    let color =
        match p.Color with
        | Some c -> string c
        | None -> "none"

    sprintf "point %d,%d (%s)" p.X p.Y color

// A task block that actually awaits: only here do we open a task { } (it really awaits).
let asyncGreet (p: Point) : Task<string> =
    task {
        let! n = jsResolve p.X
        return sprintf "%s +%d" (greet p) n
    }
