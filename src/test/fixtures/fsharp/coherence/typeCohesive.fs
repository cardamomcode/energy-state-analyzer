module TypeCohesive

// decision: mirrors the real-world F#-style Seq module pattern this fixture regression-tests
// against (expression/collections/seq.py) - one verb per operation, no shared name prefix,
// but nearly every function touches seq<'a>/seq<'b>. Must NOT be flagged as function-count
// sprawl despite exceeding the generic 12-function threshold with no naming cohesion at all.

let map (mapping: 'a -> 'b) (source: seq<'a>) : seq<'b> =
    Seq.map mapping source

let filter (predicate: 'a -> bool) (source: seq<'a>) : seq<'a> =
    Seq.filter predicate source

let choose (chooser: 'a -> 'b option) (source: seq<'a>) : seq<'b> =
    Seq.choose chooser source

let collect (mapping: 'a -> seq<'b>) (source: seq<'a>) : seq<'b> =
    Seq.collect mapping source

let concat (sources: seq<seq<'a>>) : seq<'a> =
    Seq.concat sources

let fold (folder: 'b -> 'a -> 'b) (state: 'b) (source: seq<'a>) : 'b =
    Seq.fold folder state source

let head (source: seq<'a>) : 'a =
    Seq.head source

let length (source: seq<'a>) : int =
    Seq.length source

let mapi (mapping: int -> 'a -> 'b) (source: seq<'a>) : seq<'b> =
    Seq.mapi mapping source

let pairwise (source: seq<'a>) : seq<'a> =
    Seq.pairwise source |> Seq.map fst

let scan (scanner: 'b -> 'a -> 'b) (state: 'b) (source: seq<'a>) : seq<'b> =
    Seq.scan scanner state source

let skip (count: int) (source: seq<'a>) : seq<'a> =
    Seq.skip count source

let tail (source: seq<'a>) : seq<'a> =
    Seq.tail source

let take (count: int) (source: seq<'a>) : seq<'a> =
    Seq.take count source

let distinct (source: seq<'a>) : seq<'a> =
    Seq.distinct source

let reverse (source: seq<'a>) : seq<'a> =
    Seq.rev source

// decision: tree-sitter-fsharp only parses a curried function's `: <type> =` return-type
// annotation into a clean function_declaration_left shape when something follows it in the
// file - the last such function in a module misparses as a plain value_declaration_left
// instead (silently dropping it from isFunctionDefinition entirely). This trailing binding
// exists purely so reverse above isn't the last declaration in the file.
let _sentinel = 0
