// # Position
//
// Detectors locate findings without depending on editor APIs. A lookup indexes newline starts once, then
// binary-searches each source offset into a zero-based line and column. The caller must use offsets from
// the same source snapshot; the lookup neither reparses text nor clamps invalid offsets.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.Position

/// Offset-to-line/column conversion.
///
/// decision: host-independent — it reads nothing but the source string, so the same lookup drives
/// detectors inside the VS Code extension and in the headless CLI, which has no
/// vscode.TextDocument to call positionAt on. It binary-searches precomputed line-start offsets
/// rather than delegating to a host API.
///
/// NOTE on naming: this `Position` ({ Line; Column }) is deliberately distinct from
/// TreeSitter.SourcePosition ({ Row; Column }), the raw node source position — two different
/// concepts that would otherwise collide when a detector opens both modules.

type Position = { Line: int; Column: int }

/// Document-local conversion from source offsets to zero-based lines and columns; it must be used with the
/// source from which it was built.
type PositionLookup = { toPosition: int -> Position }

/// Precompute newline offsets and return a binary-search lookup for positions in this source. The empty
/// source still has line zero; callers supply valid source offsets because this lookup does not clamp them.
let createPositionLookup (sourceText: string) : PositionLookup =
    let lineStartOffsets = ResizeArray [ 0 ]

    for i in 0 .. sourceText.Length - 1 do
        if sourceText.[i] = '\n' then
            lineStartOffsets.Add(i + 1)

    {
        toPosition =
            fun offset ->
                // Binary search for the last line-start offset <= offset (mirrors the TS lower-bound
                // search). The offsets array is monotonic, so standard binary search applies.
                let mutable low = 0
                let mutable high = lineStartOffsets.Count - 1

                while low < high do
                    // Upper midpoint ensures that advancing `low` always shrinks the interval.
                    let mid = (low + high + 1) / 2

                    if lineStartOffsets.[mid] <= offset then
                        low <- mid
                    else
                        high <- mid - 1

                {
                    Line = low
                    Column = offset - lineStartOffsets.[low]
                }
    }
