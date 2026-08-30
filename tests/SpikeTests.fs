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

// Phase 0 spike — proves the web-tree-sitter Fable binding end to end: init the parser, load
// the Python grammar, parse a real fixture, and assert on the resulting tree.
let tests =
    testList (
        "TreeSitter binding",
        [
            testAsync (
                "parses a Python fixture and reports the root node type",
                (fun _ ->
                    toAsync (
                        task {
                            let grammarPath = cwd () + "/grammars/tree-sitter-python.wasm"
                            let fixture = cwd () + "/src/test/fixtures/python/nesting.py"
                            let source = readFileSync fixture "utf8"
                            let! root = parseWith grammarPath source
                            assertThat (nodeType root) (isEqualTo "module")
                            let children = nodeNamedChildren root
                            assertThat (Array.length children) (isGreaterOrEqual 2)
                        }
                    ))
            )
        ]
    )
