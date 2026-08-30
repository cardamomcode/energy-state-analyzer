module Energy.Core.Position

// Offset-to-line/column conversion (port of src/core/position.ts).
//
// decision: host-independent — it reads nothing but the source string, so the same lookup drives
// detectors inside the VS Code extension and in the headless CLI (src/cli.ts), which has no
// vscode.TextDocument to call positionAt on. It binary-searches precomputed line-start offsets
// rather than delegating to a host API.
//
// NOTE on naming: this `Position` ({ Line; Column }) is deliberately distinct from
// TreeSitter.SourcePosition ({ Row; Column }), the raw node source position — two different
// concepts that would otherwise collide when a detector opens both modules.

type Position = { Line: int; Column: int }

type PositionLookup = { toPosition: int -> Position }

let createPositionLookup (sourceText: string) : PositionLookup =
    let lineStartOffsets = ResizeArray [ 0 ]

    for i in 0 .. sourceText.Length - 1 do
        if sourceText.[i] = '\n' then
            lineStartOffsets.Add(i + 1)

    { toPosition =
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

            { Line = low; Column = offset - lineStartOffsets.[low] } }
