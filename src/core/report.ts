import { EnergyViolation } from '../types';
import { SeverityCounts, emptyCounts } from './severityCounts';

// invariant: this module must not import fs/child_process/vscode — it only aggregates and
// renders data the caller already has, so it stays testable with hand-built fixtures
// (see src/test/integration/report.test.ts) and reusable from both the CLI's scan and diff modes
//
// decision: split into report.ts (aggregate scoring + markdown table), reportDiff.ts (base/head
// diffing), and reportHuman.ts (the human-readable risk report) — the combined file had grown to
// 16 functions across three largely independent domains, which is exactly the sprawl this tool's
// own coherence detector flags. This file re-exports the other two so existing imports of
// './report' keep working unchanged.

export interface FileResult {
    filePath: string;
    violations: EnergyViolation[];
}

export type { SeverityCounts };

export interface FileSummary {
    filePath: string;
    score: number;
    counts: SeverityCounts;
    byType: Record<string, number>;
}

export interface AggregateSummary {
    files: FileSummary[];
    totalScore: number;
    totalCounts: SeverityCounts;
}

// decision: weights (1/4/9) make high-severity violations dominate the score without parsing
// numeric complexity out of detector message strings — see src/core/detectors/cyclomatic.ts and
// cognitive.ts, which only embed their numeric complexity in `message`, not as a structured field.
// invariant: these weights are load-bearing for diff continuity (diffSummaries below) — changing
// them changes the meaning of a previously reported delta, so treat this as a versioned constant.
const SEVERITY_WEIGHT: SeverityCounts = { low: 1, medium: 4, high: 9 };

export function summarizeFile(result: FileResult): FileSummary {
    const counts = emptyCounts();
    const byType: Record<string, number> = {};

    for (const violation of result.violations) {
        counts[violation.severity] += 1;
        byType[violation.type] = (byType[violation.type] ?? 0) + 1;
    }

    const score =
        counts.low * SEVERITY_WEIGHT.low + counts.medium * SEVERITY_WEIGHT.medium + counts.high * SEVERITY_WEIGHT.high;

    return { filePath: result.filePath, score, counts, byType };
}

export function summarize(results: FileResult[]): AggregateSummary {
    const files = results.map(summarizeFile);
    const totalCounts = emptyCounts();
    let totalScore = 0;

    for (const file of files) {
        totalScore += file.score;
        totalCounts.low += file.counts.low;
        totalCounts.medium += file.counts.medium;
        totalCounts.high += file.counts.high;
    }

    return { files, totalScore, totalCounts };
}

export function hasBlockingViolations(counts: SeverityCounts): boolean {
    return counts.medium > 0 || counts.high > 0;
}

export function renderMarkdownReport(summary: AggregateSummary): string {
    const cleanCount = summary.files.filter((f) => f.score === 0).length;
    const lines: string[] = [];

    lines.push('# Energy State Report');
    lines.push('');
    lines.push(
        `**${summary.files.length} file${summary.files.length === 1 ? '' : 's'} scanned** — ${cleanCount} clean, ${summary.files.length - cleanCount} with violations`
    );
    lines.push('');
    lines.push('| File | Score | High | Medium | Low |');
    lines.push('| --- | --- | --- | --- | --- |');

    for (const file of summary.files) {
        lines.push(
            `| ${file.filePath} | ${file.score} | ${file.counts.high} | ${file.counts.medium} | ${file.counts.low} |`
        );
    }

    lines.push('');
    lines.push(
        `**Total score: ${summary.totalScore}** (${summary.totalCounts.high} high, ${summary.totalCounts.medium} medium, ${summary.totalCounts.low} low)`
    );

    return lines.join('\n');
}

export * from './reportDiff';
export * from './reportHuman';
