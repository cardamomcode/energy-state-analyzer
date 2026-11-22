// # TreeSitter
//
// This is the sole web-tree-sitter interop boundary. Shared asynchronous runtime initialization precedes
// grammar loading and parser construction; parsing and node traversal are synchronous once a grammar is
// installed. Accessors borrow nodes from a live tree. Production analysis uses withParsedTree to release
// native tree memory in a finally block; parseWith is a convenience returning a live root and does not
// perform that cleanup.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.TreeSitter

open System.Threading.Tasks

open Fable.Core
open Energy.Core.Paths

// Binding facade.

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

/// Imported Parser constructor used for runtime initialization and configured parser instances.

[<Import("Parser", "web-tree-sitter")>]
let parserCtor: obj = nativeOnly

/// Imported grammar loader used after the shared tree-sitter runtime initializes.
[<Import("Language", "web-tree-sitter")>]
let languageCtor: obj = nativeOnly

// ---------------------------------------------------------------------------
// Async entry points (promise-based -> Task<'T> = native Promise)
// ---------------------------------------------------------------------------

/// `Parser.init()` — one-time WASM bootstrap. Promise<void>.
[<Emit("$0.init()")>]
let private initialize (ctor: obj) : Task<unit> = nativeOnly

/// Share WASM initialization across concurrent grammar requests.
///
/// decision: caches the pending task, because concurrent Parser.init calls can create separate
/// WASM heaps before the upstream runtime has cached its initialized module.
let private initialization = lazy (initialize parserCtor)

/// Await the one shared parser runtime before constructing parsers or loading languages.
let init () : Task<unit> = initialization.Value

/// `Language.load(path)` — load a grammar WASM from a path. Promise<Language>.
///
/// decision: the WASM path is a Core.Paths.Path like every other host-filesystem path binding.
[<Emit("$0.load($1)")>]
let load (ctor: obj) (path: Path) : Task<Grammar> = nativeOnly

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

/// Release the native memory owned by a syntax tree.
[<Emit("$0.delete()")>]
let deleteTree (tree: Tree) : unit = nativeOnly

/// Consume a parsed tree synchronously and release its native memory even if analysis fails.
///
/// invariant: the callback returns detached data; nodes must not escape the tree's lifetime.
let withParsedTree parser source consume =
    let tree = parse parser source

    try
        consume (rootNode tree)
    finally
        deleteTree tree

/// Read the grammar kind of a live node as the erased NodeType wrapper.

[<Emit("$0.type")>]
let nodeType (node: Node) : NodeType = nativeOnly

/// Read the exact source text covered by a node while its tree is alive.
[<Emit("$0.text")>]
let nodeText (node: Node) : string = nativeOnly

/// Literal text of a leaf/terminal node (`undefined` on internal nodes — read only on leaves).
[<Emit("$0.value")>]
let nodeValue (node: Node) : string = nativeOnly

/// Report whether a node is named by the grammar rather than an anonymous punctuation token.
[<Emit("$0.isNamed")>]
let nodeIsNamed (node: Node) : bool = nativeOnly

/// Report parser errors anywhere below a node, including missing required syntax.
[<Emit("$0.hasError")>]
let nodeHasError (node: Node) : bool = nativeOnly

/// Read the identity of a node within its live tree for matching wrappers, operands, and branches.
[<Emit("$0.id")>]
let nodeId (node: Node) : int = nativeOnly

/// Read the node's starting source offset for the document position lookup.
[<Emit("$0.startIndex")>]
let nodeStartIndex (node: Node) : int = nativeOnly

/// Read the exclusive ending source offset of a live node.
[<Emit("$0.endIndex")>]
let nodeEndIndex (node: Node) : int = nativeOnly

/// Positions surfaced as typed records. The raw row/column accessors below are the individual
/// `<Emit>` reads; the record builders compose them so callers see a `Position`.
[<Emit("$0.startPosition.row")>]
let nodeStartRow (node: Node) : int = nativeOnly

/// Read the zero-based column where a live node starts.
[<Emit("$0.startPosition.column")>]
let nodeStartColumn (node: Node) : int = nativeOnly

/// Read the zero-based row of the node's exclusive end position.
[<Emit("$0.endPosition.row")>]
let nodeEndRow (node: Node) : int = nativeOnly

/// Read the zero-based column of the node's exclusive end position.
[<Emit("$0.endPosition.column")>]
let nodeEndColumn (node: Node) : int = nativeOnly

/// Combine the live node's start row and column into a detached source position.
let nodeStartPosition (node: Node) : SourcePosition =
    {
        Row = nodeStartRow node
        Column = nodeStartColumn node
    }

/// Combine the live node's exclusive end row and column into a detached source position.
let nodeEndPosition (node: Node) : SourcePosition =
    {
        Row = nodeEndRow node
        Column = nodeEndColumn node
    }

/// Children surfaced as F# lists — idiomatic for the detectors' List folds/patterns, and it
/// avoids the "empty-array ceremony" the coherence detector itself flags (§3.2). The JS
/// `.children`/`.namedChildren` are arrays; convert once at the accessor boundary.
[<Emit("$0.children")>]
let nodeChildrenRaw (node: Node) : Node[] = nativeOnly

/// Return all direct children in grammar order as an F# list, including punctuation; nodes remain borrowed
/// from the live tree.
let nodeChildren (node: Node) : Node list = nodeChildrenRaw node |> Array.toList

/// Read the native array of direct named children, excluding anonymous grammar tokens.
[<Emit("$0.namedChildren")>]
let nodeNamedChildrenRaw (node: Node) : Node[] = nativeOnly

/// Return direct named children in source order as an F# list; child nodes retain the tree's lifetime
/// requirement.
let nodeNamedChildren (node: Node) : Node list =
    nodeNamedChildrenRaw node |> Array.toList

/// Read the child at a zero-based index; callers must supply a valid index because the raw binding can
/// return null.
[<Emit("$0.child($1)")>]
let nodeChild (node: Node) (index: int) : Node = nativeOnly

/// Look up a grammar field, preserving missing fields as None at the facade boundary.
[<Emit("$0.childForFieldName($1)")>]
let private nodeFieldRaw (node: Node) (field: string) : obj = nativeOnly

/// Return the child occupying a named grammar field, if present.
let nodeField (field: string) (node: Node) : Node option =
    match nodeFieldRaw node field with
    | null -> None
    | child -> Some child

/// Read the native array of every child occupying a repeatable grammar field.
[<Emit("$0.childrenForFieldName($1)")>]
let private nodeFieldChildrenRaw (node: Node) (field: string) : Node[] = nativeOnly

/// Return every child occupying a named grammar field in source order; a field may repeat (Kotlin's
/// `when` entry carries one `condition` per comma-separated alternative).
let nodeFieldChildren (field: string) (node: Node) : Node list =
    nodeFieldChildrenRaw node field |> Array.toList

/// Parent as an Option: the root node's parent is null, which becomes None. Reads the member
/// once and maps null -> None at the boundary.
[<Emit("$0.parent")>]
let nodeParentRaw (node: Node) : obj = nativeOnly

/// Return the parent of a live node, or None for a root node.
let nodeParent (node: Node) : Node option =
    match nodeParentRaw node with
    | null -> None
    | p -> Some p

/// Return the preceding named sibling, if one exists.
[<Emit("$0.previousNamedSibling")>]
let private nodePreviousNamedSiblingRaw (node: Node) : obj = nativeOnly

/// Return the preceding named sibling, or None when no such sibling exists.
let nodePreviousNamedSibling (node: Node) : Node option =
    match nodePreviousNamedSiblingRaw node with
    | null -> None
    | sibling -> Some sibling

// ---------------------------------------------------------------------------
// Convenience: load a grammar and parse in one promise chain
// ---------------------------------------------------------------------------

/// Load a grammar and parse source text in one awaited promise chain.
///
/// decision: convenience for the CLI and tests — inits the parser, loads the grammar, and
/// parses, all in one task { } block (each step really awaits, so the task block is warranted).
/// The extension uses the low-level bindings directly so it can cache the loaded grammar/parser
/// across documents (see Grammar.fs in Phase 3).
/// decision: the grammar path is a Core.Paths.Path, so this no longer presents a
/// swappable string/string parameter pair.
let parseWith (grammarPath: Path) (source: string) : Task<Node> =
    task {
        do! init ()
        let! grammar = load languageCtor grammarPath
        let parser = makeParser parserCtor
        setLanguage parser grammar |> ignore
        let tree = parse parser source
        return rootNode tree
    }
