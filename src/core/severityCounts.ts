// decision: split out of report.ts so the low/medium/high tally shape and its zero-value
// constructor can be shared by report.ts's aggregate scoring and reportHuman.ts's human-readable
// rendering without either module importing the other (see report.ts for the coherence split)
export type SeverityCounts = Record<'low' | 'medium' | 'high', number>;

export function emptyCounts(): SeverityCounts {
    return { low: 0, medium: 0, high: 0 };
}
