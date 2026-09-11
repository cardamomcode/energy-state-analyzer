module Energy.Tests.SpikeTests

open System.Threading.Tasks

open Fable.Core.JsInterop

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Core.TreeSitter

// Phase 0 spike — proves the web-tree-sitter Fable binding end to end, and that the typed
// facade (Position records, Node lists, Parent option) surfaces the tree as pure F# values.
let tests =
    testList (
        "TreeSitter binding",
        [ test (
              "releases a parsed tree after consuming its root",
              fun _ ->
                  let mutable deleted = false
                  let root = createObj [ "type" ==> "module" ]

                  let tree =
                      createObj [ "rootNode" ==> root; "delete" ==> (fun () -> deleted <- true) ]

                  let parser = createObj [ "parse" ==> (fun (_: string) -> tree) ]

                  let result = withParsedTree parser "source" nodeType
                  assertThat result (isEqualTo (NodeType "module"))
                  assertThat deleted isTrue
          )
          test (
              "releases a parsed tree when its consumer throws",
              fun _ ->
                  let mutable deleted = false

                  let tree =
                      createObj [ "rootNode" ==> obj (); "delete" ==> (fun () -> deleted <- true) ]

                  let parser = createObj [ "parse" ==> (fun (_: string) -> tree) ]
                  let mutable message = ""

                  try
                      withParsedTree parser "source" (fun _ -> failwith "consumer failed")
                  with error ->
                      message <- error.Message

                  assertThat message (isEqualTo "consumer failed")
                  assertThat deleted isTrue
          )
          testAsync (
              "parses a Python fixture and reports the root node type",
              (fun _ ->
                  toAsync (
                      task {
                          let grammarPath = cwd () + "/grammars/tree-sitter-python.wasm"
                          let fixture = cwd () + "/src/test/fixtures/python/nesting.py"
                          let source = readFileSync (Path fixture) (Encoding "utf8")
                          let! root = parseWith (Path grammarPath) source
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
                          let source = readFileSync (Path fixture) (Encoding "utf8")
                          let! root = parseWith (Path grammarPath) source
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
                          let source = readFileSync (Path fixture) (Encoding "utf8")
                          let! root = parseWith (Path grammarPath) source
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
