module Energy.Tests.HelloTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Hello

// Phase 0 spike tests — prove the record, the DU, and a task { } block awaiting a native
// JS promise, bridged into Quill's Async test bodies. Deleted in Phase 1.
//
// decision: F# static members (Test.test/testAsync/testList) are called with tuple
// syntax, not function-application syntax — partial application of a static member is
// not allowed, so `testList "name" [...]` fails to resolve.
let tests =
    testList (
        "Hello",
        [ test (
              "sync greet renders the record and the DU",
              (fun _ ->
                  let p = { X = 1; Y = 2; Color = Some Green }
                  assertThat (greet p) (isEqualTo "point 1,2 (Green)"))
          )
          test (
              "sync greet with no color",
              (fun _ ->
                  let p = { X = 3; Y = 4; Color = None }
                  assertThat (greet p) (isEqualTo "point 3,4 (none)"))
          )
          testAsync (
              "async greet awaits a native promise",
              (fun _ ->
                  toAsync (
                      task {
                          let! g = asyncGreet { X = 5; Y = 6; Color = Some Red }
                          assertThat g (isEqualTo "point 5,6 (Red) +5")
                      }
                  ))
          ) ]
    )
