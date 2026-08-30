module Energy.Tests.SpikeTests

open System.Threading.Tasks

open Fable.Core
open Fable.Core.JsInterop

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter

// Node-only plumbing for the Phase 0 spike (cwd + file read). Reorganized into the CLI's
// Node binding (Fable.Node) in Phase 2.
[<Emit("process.cwd()")>]
let cwd () : string = nativeOnly

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: string) (encoding: string) : string = nativeOnly

// Phase 0 spike — proves the web-tree-sitter Fable binding end to end, and that the typed
// facade (Position records, Node lists, Parent option) surfaces the tree as pure F# values.
let tests =
    testList (
        "TreeSitter binding",
        [ testAsync (
              "parses a Python fixture and reports the root node type",
              (fun _ ->
                  toAsync (
                      task {
                          let grammarPath = cwd () + "/grammars/tree-sitter-python.wasm"
                          let fixture = cwd () + "/src/test/fixtures/python/nesting.py"
                          let source = readFileSync fixture "utf8"
                          let! root = parseWith grammarPath source
                          assertThat (nodeType root) (isEqualTo (NodeType "module"))
                      }
                  ))
          )
          testAsync (
              "surfaces positions as typed records",
              (fun _ ->
                  toAsync (
                      task {
                          let grammarPath = cwd () + "/grammars/tree-sitter-python.wasm"
                          let fixture = cwd () + "/src/test/fixtures/python/nesting.py"
                          let source = readFileSync fixture "utf8"
                          let! root = parseWith grammarPath source
                          // The root sits at the origin; asserting fields proves the Position
                          // record is built from the raw row/column reads.
                          assertThat (nodeStartPosition root).Row (isEqualTo 0)
                          assertThat (nodeStartPosition root).Column (isEqualTo 0)
                          assertThat (nodeEndPosition root).Row (isGreaterOrEqual 0)
                      }
                  ))
          )
          testAsync (
              "surfaces children as F# lists and parent as an option",
              (fun _ ->
                  toAsync (
                      task {
                          let grammarPath = cwd () + "/grammars/tree-sitter-python.wasm"
                          let fixture = cwd () + "/src/test/fixtures/python/nesting.py"
                          let source = readFileSync fixture "utf8"
                          let! root = parseWith grammarPath source
                          // Lists, not arrays — idiomatic for the detectors' List folds.
                          assertThat (List.length (nodeChildren root)) (isGreaterOrEqual 2)
                          assertThat (List.length (nodeNamedChildren root)) (isGreaterOrEqual 2)
                          // The root has no parent -> Option.toList is empty...
                          assertThat (Option.toList (nodeParent root) |> List.length) (isEqualTo 0)
                          // ...while a named child sees exactly one parent.
                          match nodeNamedChildren root with
                          | first :: _ ->
                              assertThat (nodeIsNamed first) (isEqualTo true)
                              assertThat (Option.toList (nodeParent first) |> List.length) (isEqualTo 1)
                          | [] -> assertThat false (isTrue)
                      }
                  ))
          ) ]
    )
