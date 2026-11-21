// # Helpers
//
// This auto-open helper bridges native Fable tasks to the Async values expected by Quill test callbacks.
// Awaiting preserves task success or failure; it introduces no parsing, assertions, or separate test
// cases. Main.fs registers the suites that use it.
//
// Reading path: [Source guide](../docs/source-guide.md).

namespace Energy.Tests

open System.Threading.Tasks

// Shared plumbing for the Scriptorium suite. Assertions and the runner come from
// Scriptorium (Nib + Quill); what is left here is the glue to hand a `Task` (native
// Promise) to Quill, whose async test bodies speak `Async`. Copied idiom from the local
// Fable.Giraffe reference (test/shared/Helpers.fs).
[<AutoOpen>]
module Helpers =

    /// Bridge a `Task` (native Promise) into the `Async` that Quill's `testAsync` expects.
    let toAsync (t: Task<'a>) : Async<'a> = Async.AwaitTask t
