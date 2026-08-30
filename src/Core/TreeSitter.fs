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
// every Node member) is bound as ordinary values — no Task wrapper.
// invariant: this module is the only place that touches web-tree-sitter; detectors and
// languages see only the typed accessors below, never the raw JS object. Nodes stay Fable
// dynamic (`obj`) with a typed accessor layer — mirrors the current TS, which reads `.type`/
// `.text`/`.children`/... on `any` nodes — so detectors get pure F# signatures with no `any`.

/// A live web-tree-sitter `Node` (a JS object). Kept as `obj` (Fable dynamic) with the typed
/// accessors below; never constructed by our code, only returned from the bindings.
type Node = obj

/// A live web-tree-sitter `Tree` (a JS object).
type Tree = obj

/// A loaded web-tree-sitter `Language`/grammar (a JS object).
type Grammar = obj

/// A web-tree-sitter `Parser` instance (a JS object).
type Parser = obj

/// A grammar node-type value returned by web-tree-sitter.
///
/// `NodeType` is erased to its backing string in generated JavaScript, so comparisons and adapter
/// tables preserve the prior runtime representation while preventing grammar names from mixing with
/// arbitrary source text in F#.
///
/// decision: uses an erased single-case union so grammar-specific names are type-safe in F# without
/// changing web-tree-sitter's string-valued JavaScript API.
/// invariant: every `NodeType` value has exactly its wrapped string as its JavaScript representation.
[<Erase>]
type NodeType = NodeType of string

/// A zero-based source position (row, column) as web-tree-sitter reports it for a node. The
/// kind of typed value the facade returns instead of raw JS numbers — detectors pattern-match on
/// records, not on dynamic members. Named `SourcePosition` (not just `Position`) to stay distinct
/// from Core.Position ({ Line; Column }), the offset lookup result both share when opened together.
type SourcePosition = { Row: int; Column: int }

// ---------------------------------------------------------------------------
// Module-level named imports (Parser and Language classes)
// ---------------------------------------------------------------------------

[<Import("Parser", "web-tree-sitter")>]
let parserCtor: obj = nativeOnly

[<Import("Language", "web-tree-sitter")>]
let languageCtor: obj = nativeOnly

// ---------------------------------------------------------------------------
// Async entry points (promise-based -> Task<'T> = native Promise)
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
// Typed node accessors (live JS object members, surfaced as typed F# values)
// ---------------------------------------------------------------------------

[<Emit("$0.type")>]
let nodeType (node: Node) : NodeType = nativeOnly

[<Emit("$0.text")>]
let nodeText (node: Node) : string = nativeOnly

/// Literal text of a leaf/terminal node (`undefined` on internal nodes — read only on leaves).
[<Emit("$0.value")>]
let nodeValue (node: Node) : string = nativeOnly

[<Emit("$0.isNamed")>]
let nodeIsNamed (node: Node) : bool = nativeOnly

/// Stable node id (used by the position lookup to map nodes back to source ranges).
[<Emit("$0.id")>]
let nodeId (node: Node) : int = nativeOnly

[<Emit("$0.startIndex")>]
let nodeStartIndex (node: Node) : int = nativeOnly

[<Emit("$0.endIndex")>]
let nodeEndIndex (node: Node) : int = nativeOnly

// Positions surfaced as typed records. The raw row/column accessors below are the individual
// `<Emit>` reads; the record builders compose them so callers see a `Position`.
[<Emit("$0.startPosition.row")>]
let nodeStartRow (node: Node) : int = nativeOnly

[<Emit("$0.startPosition.column")>]
let nodeStartColumn (node: Node) : int = nativeOnly

[<Emit("$0.endPosition.row")>]
let nodeEndRow (node: Node) : int = nativeOnly

[<Emit("$0.endPosition.column")>]
let nodeEndColumn (node: Node) : int = nativeOnly

let nodeStartPosition (node: Node) : SourcePosition =
    { Row = nodeStartRow node
      Column = nodeStartColumn node }

let nodeEndPosition (node: Node) : SourcePosition =
    { Row = nodeEndRow node
      Column = nodeEndColumn node }

// Children surfaced as F# lists — idiomatic for the detectors' List folds/patterns, and it
// avoids the "empty-array ceremony" the coherence detector itself flags (§3.2). The JS
// `.children`/`.namedChildren` are arrays; convert once at the accessor boundary.
[<Emit("$0.children")>]
let nodeChildrenRaw (node: Node) : Node[] = nativeOnly

let nodeChildren (node: Node) : Node list = nodeChildrenRaw node |> Array.toList

[<Emit("$0.namedChildren")>]
let nodeNamedChildrenRaw (node: Node) : Node[] = nativeOnly

let nodeNamedChildren (node: Node) : Node list =
    nodeNamedChildrenRaw node |> Array.toList

[<Emit("$0.child($1)")>]
let nodeChild (node: Node) (index: int) : Node = nativeOnly

// Parent as an Option: the root node's parent is null, which becomes None. Reads the member
// once and maps null -> None at the boundary.
[<Emit("$0.parent")>]
let nodeParentRaw (node: Node) : obj = nativeOnly

let nodeParent (node: Node) : Node option =
    match nodeParentRaw node with
    | null -> None
    | p -> Some p

// ---------------------------------------------------------------------------
// Convenience: load a grammar and parse in one promise chain
// ---------------------------------------------------------------------------

// decision: convenience for the CLI and tests — inits the parser, loads the grammar, and
// parses, all in one task { } block (each step really awaits, so the task block is warranted).
// The extension uses the low-level bindings directly so it can cache the loaded grammar/parser
// across documents (see Grammar.fs in Phase 3).
let parseWith (grammarPath: string) (source: string) : Task<Node> =
    task {
        do! init parserCtor
        let! grammar = load languageCtor grammarPath
        let parser = makeParser parserCtor
        setLanguage parser grammar |> ignore
        let tree = parse parser source
        return rootNode tree
    }
