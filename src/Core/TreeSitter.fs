module Energy.Core.TreeSitter

open System.Threading.Tasks

open Fable.Core
open Fable.Core.JsInterop

// web-tree-sitter (0.26) is an ES module (with a CJS build for `require`) that exposes the
// named exports `Parser` and `Language`. Named imports resolve under both Node ESM and
// webpack's CJS/ESM interop, so we bind them as named imports rather than importAll + dynamic
// member access.
//
// decision: promise-based entry points (Parser.init, Language.load) are bound as Task<'T>
// (Fable maps Task<'T> to a native Promise on the JS target), so they are awaited with plain
// let!/do! inside task { } blocks. The synchronous API (new Parser, setLanguage, parse, and
// every Node/Tree member) is bound as ordinary values — no Task wrapper.
// invariant: this module is the only place that touches web-tree-sitter; the detectors and
// languages see only the typed Node/Tree accessors below, never the raw JS object.

/// A live web-tree-sitter `Node` (a JS object). Kept as `obj` (Fable dynamic) with the typed
/// accessors below — mirrors the current TS code, which treats nodes as `any` and reads
/// `.type`/`.text`/`.children`/... directly.
type Node = obj

/// A live web-tree-sitter `Tree` (a JS object).
type Tree = obj

/// A loaded web-tree-sitter `Language` (a JS object).
type Grammar = obj

/// A web-tree-sitter `Parser` instance (a JS object).
type Parser = obj

// ---------------------------------------------------------------------------
// Module-level named imports (Parser and Language classes)
// ---------------------------------------------------------------------------

[<Import("Parser", "web-tree-sitter")>]
let parserCtor: obj = nativeOnly

[<Import("Language", "web-tree-sitter")>]
let languageCtor: obj = nativeOnly

// ---------------------------------------------------------------------------
// Async entry points (promise-based → Task<'T> = native Promise)
// ---------------------------------------------------------------------------

/// `Parser.init()` — one-time WASM bootstrap. Promise<void>.
[<Emit("$0.init()")>]
let init (ctor: obj) : Task<unit> = nativeOnly

/// `Language.load(path)` — load a grammar WASM from a path. Promise<Language>.
[<Emit("$0.load($1)")>]
let load (ctor: obj) (path: string) : Task<Grammar> = nativeOnly

// ---------------------------------------------------------------------------
// Synchronous parser lifecycle
// ---------------------------------------------------------------------------

/// `new Parser()` — a fresh parser instance.
[<Emit("new $0()")>]
let makeParser (ctor: obj) : Parser = nativeOnly

/// `parser.setLanguage(grammar)` — returns the parser.
[<Emit("$0.setLanguage($1)")>]
let setLanguage (parser: Parser) (grammar: Grammar) : Parser = nativeOnly

/// `parser.parse(text)` — parse source text into a Tree (synchronous once the grammar is set).
[<Emit("$0.parse($1)")>]
let parse (parser: Parser) (text: string) : Tree = nativeOnly

/// `tree.rootNode` — the root Node of a parsed tree.
[<Emit("$0.rootNode")>]
let rootNode (tree: Tree) : Node = nativeOnly

// ---------------------------------------------------------------------------
// Node accessors (live JS object members, surfaced as typed values)
// ---------------------------------------------------------------------------

[<Emit("$0.type")>]
let nodeType (node: Node) : string = nativeOnly

[<Emit("$0.text")>]
let nodeText (node: Node) : string = nativeOnly

[<Emit("$0.isNamed")>]
let nodeIsNamed (node: Node) : bool = nativeOnly

[<Emit("$0.children")>]
let nodeChildren (node: Node) : Node [] = nativeOnly

[<Emit("$0.namedChildren")>]
let nodeNamedChildren (node: Node) : Node [] = nativeOnly

[<Emit("$0.child($1)")>]
let nodeChild (node: Node) (index: int) : Node = nativeOnly

[<Emit("$0.parent")>]
let nodeParent (node: Node) : Node = nativeOnly

[<Emit("$0.id")>]
let nodeId (node: Node) : int = nativeOnly

/// Byte offset of the node start (used with the position lookup, mirroring TS `.startIndex`).
[<Emit("$0.startIndex")>]
let nodeStartIndex (node: Node) : int = nativeOnly

/// Byte offset of the node end (mirroring TS `.endIndex`).
[<Emit("$0.endIndex")>]
let nodeEndIndex (node: Node) : int = nativeOnly

/// Zero-based row of the node start.
[<Emit("$0.startPosition.row")>]
let nodeStartRow (node: Node) : int = nativeOnly

/// Zero-based column of the node start.
[<Emit("$0.startPosition.column")>]
let nodeStartColumn (node: Node) : int = nativeOnly

/// Zero-based row of the node end.
[<Emit("$0.endPosition.row")>]
let nodeEndRow (node: Node) : int = nativeOnly

/// Zero-based column of the node end.
[<Emit("$0.endPosition.column")>]
let nodeEndColumn (node: Node) : int = nativeOnly

// ---------------------------------------------------------------------------
// Convenience: load a grammar and parse in one promise chain
// ---------------------------------------------------------------------------

// decision: convenience for the CLI and the Phase 0 spike — inits the parser, loads the
// grammar, and parses, all in one task { } block (each step really awaits, so the task block
// is warranted). The extension uses the low-level bindings directly so it can cache the
// loaded grammar/parser across documents (see Grammar.fs in Phase 3).
let parseWith (grammarPath: string) (source: string) : Task<Node> =
    task {
        do! init parserCtor
        let! grammar = load languageCtor grammarPath
        let parser = makeParser parserCtor
        setLanguage parser grammar |> ignore
        let tree = parse parser source
        return rootNode tree
    }
