// Offset-to-line/column conversion that doesn't depend on vscode.TextDocument,
// so detectors can run both inside the extension and in a headless CLI.
//
// decision: binary-searches precomputed line-start offsets instead of using vscode.TextDocument.positionAt — keeps detectors host-independent (see cli.ts)

export interface Position {
    line: number;
    column: number;
}

export interface PositionLookup {
    toPosition(offset: number): Position;
}

export function createPositionLookup(sourceText: string): PositionLookup {
    const lineStartOffsets: number[] = [0];
    for (let i = 0; i < sourceText.length; i++) {
        if (sourceText[i] === '\n') {
            lineStartOffsets.push(i + 1);
        }
    }

    return {
        toPosition(offset: number): Position {
            let low = 0;
            let high = lineStartOffsets.length - 1;
            while (low < high) {
                const mid = Math.ceil((low + high) / 2);
                if (lineStartOffsets[mid] <= offset) {
                    low = mid;
                } else {
                    high = mid - 1;
                }
            }
            return { line: low, column: offset - lineStartOffsets[low] };
        }
    };
}
