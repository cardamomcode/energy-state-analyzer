// decision: split out of report.ts (base/head diffing and its markdown rendering) so the
// aggregate-scoring, diff, and human-report domains each live in their own file — see the
// coherence note in report.ts
import type { FileSummary } from './report';

export interface DiffEntry {
    filePath: string;
    baseScore: number | null;
    headScore: number;
    delta: number;
    status: 'new' | 'improved' | 'worsened' | 'unchanged';
}

// decision: keyed by filePath so callers can pass base/head summaries independently sized -
// a file present only in head (baseScore null) is reported as 'new' rather than dropped
export function diffSummaries(base: FileSummary[], head: FileSummary[]): DiffEntry[] {
    const baseByPath = new Map(base.map((f) => [f.filePath, f.score]));

    return head.map((file) => {
        const baseScore = baseByPath.get(file.filePath) ?? null;
        const delta = baseScore === null ? file.score : file.score - baseScore;
        const status: DiffEntry['status'] =
            baseScore === null ? 'new' : delta < 0 ? 'improved' : delta > 0 ? 'worsened' : 'unchanged';

        return { filePath: file.filePath, baseScore, headScore: file.score, delta, status };
    });
}

const STATUS_ICON: Record<DiffEntry['status'], string> = {
    new: '🆕',
    improved: '🟢',
    worsened: '🔴',
    unchanged: '⚪'
};

const EMPTY_STATUS_COUNTS: Record<DiffEntry['status'], number> = { new: 0, improved: 0, worsened: 0, unchanged: 0 };

export function renderDiffMarkdown(entries: DiffEntry[], baseRef: string): string {
    const lines: string[] = [];

    lines.push(`# Energy State Diff vs \`${baseRef}\``);
    lines.push('');
    lines.push('| File | Base | Head | Δ | Status |');
    lines.push('| --- | --- | --- | --- | --- |');

    for (const entry of entries) {
        const base = entry.baseScore === null ? '—' : String(entry.baseScore);
        const delta = entry.baseScore === null ? '—' : entry.delta > 0 ? `+${entry.delta}` : String(entry.delta);
        lines.push(
            `| ${entry.filePath} | ${base} | ${entry.headScore} | ${delta} | ${STATUS_ICON[entry.status]} ${entry.status} |`
        );
    }

    // decision: tallies every status in one pass over a Record rather than three separate
    // `entries.filter(e => e.status === '...').length` calls — besides being cheaper, it avoids
    // repeating e.status against string literals, which the primitive-obsession detector reads
    // as stringly-typed control flow even though DiffEntry['status'] is already a literal union
    const statusCounts = entries.reduce(
        (counts, entry) => {
            counts[entry.status] += 1;
            return counts;
        },
        { ...EMPTY_STATUS_COUNTS }
    );

    lines.push('');
    lines.push(
        `_${entries.length} file${entries.length === 1 ? '' : 's'} changed, ${statusCounts.worsened} worsened, ${statusCounts.improved} improved, ${statusCounts.new} new._`
    );

    return lines.join('\n');
}
