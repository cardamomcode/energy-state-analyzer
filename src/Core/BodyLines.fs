// # BodyLines
//
// Recovery-size checks need executable source lines rather than raw text spans. This helper excludes blank
// and comment-only lines inside a syntax body, preserving lines that still contain code. ErrorShadowing.fs
// uses the resulting count for individual recovery-body limits.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.BodyLines

open Energy.Core.TreeSitter
open Energy.Core.Position

/// Count distinct nonblank source lines in body syntax after removing parsed comments.
///
/// decision: body ranges exclude clause headers and outer delimiters; nested syntax remains in
/// the ranges so a multiline loop cannot masquerade as a trivial protected call.
let count (source: string) (positions: PositionLookup) (items: Node list) : int =
    /// Collect complete comment spans recursively so characters inside multiline comments cannot count
    /// as executable work.
    let rec commentRanges node =
        if List.contains (nodeType node) [ NodeType "comment"; NodeType "line_comment"; NodeType "block_comment" ] then
            [ (nodeStartIndex node, nodeEndIndex node) ]
        else
            nodeNamedChildren node |> List.collect commentRanges

    items
    |> List.collect (fun item ->
        let comments = commentRanges item

        [ nodeStartIndex item .. nodeEndIndex item - 1 ]
        |> List.filter (fun offset ->
            not (System.Char.IsWhiteSpace source.[offset])
            && not (
                comments
                |> List.exists (fun (start, finish) -> offset >= start && offset < finish)
            ))
        |> List.map (fun offset -> (positions.toPosition offset).Line))
    |> Set.ofList
    |> Set.count
