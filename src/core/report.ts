import { EnergyViolation, SEVERITY, VIOLATION_TYPE } from '../types';

// invariant: this module must not import fs/child_process/vscode — it only aggregates and
// renders data the caller already has, so it stays testable with hand-built fixtures
// (see src/test/integration/report.test.ts) and reusable from both the CLI's scan and diff modes

export interface FileResult {
    filePath: string;
    violations: EnergyViolation[];
}

export type SeverityCounts = Record<'low' | 'medium' | 'high', number>;

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

function emptyCounts(): SeverityCounts {
    return { low: 0, medium: 0, high: 0 };
}

export function summarizeFile(result: FileResult): FileSummary {
    const counts = emptyCounts();
    const byType: Record<string, number> = {};

    for (const violation of result.violations) {
        counts[violation.severity] += 1;
        byType[violation.type] = (byType[violation.type] ?? 0) + 1;
    }

    const score =
        counts.low * SEVERITY_WEIGHT.low +
        counts.medium * SEVERITY_WEIGHT.medium +
        counts.high * SEVERITY_WEIGHT.high;

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
    const cleanCount = summary.files.filter(f => f.score === 0).length;
    const lines: string[] = [];

    lines.push('# Energy State Report');
    lines.push('');
    lines.push(`**${summary.files.length} file${summary.files.length === 1 ? '' : 's'} scanned** — ${cleanCount} clean, ${summary.files.length - cleanCount} with violations`);
    lines.push('');
    lines.push('| File | Score | High | Medium | Low |');
    lines.push('| --- | --- | --- | --- | --- |');

    for (const file of summary.files) {
        lines.push(`| ${file.filePath} | ${file.score} | ${file.counts.high} | ${file.counts.medium} | ${file.counts.low} |`);
    }

    lines.push('');
    lines.push(`**Total score: ${summary.totalScore}** (${summary.totalCounts.high} high, ${summary.totalCounts.medium} medium, ${summary.totalCounts.low} low)`);

    return lines.join('\n');
}

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
    const baseByPath = new Map(base.map(f => [f.filePath, f.score]));

    return head.map(file => {
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

export function renderDiffMarkdown(entries: DiffEntry[], baseRef: string): string {
    const lines: string[] = [];

    lines.push(`# Energy State Diff vs \`${baseRef}\``);
    lines.push('');
    lines.push('| File | Base | Head | Δ | Status |');
    lines.push('| --- | --- | --- | --- | --- |');

    for (const entry of entries) {
        const base = entry.baseScore === null ? '—' : String(entry.baseScore);
        const delta = entry.baseScore === null ? '—' : (entry.delta > 0 ? `+${entry.delta}` : String(entry.delta));
        lines.push(`| ${entry.filePath} | ${base} | ${entry.headScore} | ${delta} | ${STATUS_ICON[entry.status]} ${entry.status} |`);
    }

    const worsened = entries.filter(e => e.status === 'worsened').length;
    const improved = entries.filter(e => e.status === 'improved').length;
    const added = entries.filter(e => e.status === 'new').length;

    lines.push('');
    lines.push(`_${entries.length} file${entries.length === 1 ? '' : 's'} changed, ${worsened} worsened, ${improved} improved, ${added} new._`);

    return lines.join('\n');
}

// --- Human-readable report -------------------------------------------------
//
// decision: risk is reported on the CVSS (Common Vulnerability Scoring System) qualitative
// scale (None/Low/Medium/High/Critical) rather than a bespoke label set — it's a scale
// developers already carry an intuition for from CVEs, so "High" or "Critical" here means
// roughly what it means in a vulnerability report, not a scale a reader has to learn fresh.
// decision: the underlying 0.0-10.0 CVSS-equivalent score is a piecewise-linear mapping of
// the raw complexity number, anchored at README's existing McCabe breakpoints (10/20/50) —
// see complexityToCvssScore below — so the CVSS label is a re-expression of the same,
// already-documented complexity bands rather than a second, independently-calibrated scale.
// decision: a file's/report's headline risk is the MAXIMUM complexity value found, not an
// average — averaging a file's function complexities would let one severely complex function
// hide behind many trivial ones (nine functions at complexity 2 and one at 60 averages to ~8,
// "Low"), masking exactly the function worth fixing first. Violation *counts* are reported
// separately as a breadth indicator, deliberately not folded into the same number — this
// mirrors the README's own Energy (peak intensity) vs Entropy (how spread out) distinction.

export type RiskLevel = 'none' | 'low' | 'medium' | 'high' | 'critical';

// decision: anchors (value, cvssScore) pairs at the exact breakpoints README already
// documents for cyclomatic complexity (10/20/50), reusing the official CVSS v3.1 qualitative
// boundaries (3.9/6.9/8.9/10.0) as the corresponding output scores, then linearly interpolates
// between anchors and caps at complexity 100+ = 10.0 (CVSS has no score above 10.0)
const CVSS_CURVE: { value: number; score: number }[] = [
    { value: 0, score: 0.0 },
    { value: 10, score: 3.9 },
    { value: 20, score: 6.9 },
    { value: 50, score: 8.9 },
    { value: 100, score: 10.0 }
];

export function complexityToCvssScore(value: number): number {
    if (value <= 0) {
        return 0;
    }
    if (value >= 100) {
        return 10.0;
    }
    for (let i = 1; i < CVSS_CURVE.length; i++) {
        const prev = CVSS_CURVE[i - 1];
        const next = CVSS_CURVE[i];
        if (value <= next.value) {
            const ratio = (value - prev.value) / (next.value - prev.value);
            return Math.round((prev.score + ratio * (next.score - prev.score)) * 10) / 10;
        }
    }
    return 10.0;
}

// decision: boundaries match the official CVSS v3.1 qualitative severity rating scale exactly
// (None 0.0, Low 0.1-3.9, Medium 4.0-6.9, High 7.0-8.9, Critical 9.0-10.0)
export function classifyCvssScore(score: number): RiskLevel {
    if (score <= 0) {
        return 'none';
    }
    if (score < 4.0) {
        return 'low';
    }
    if (score < 7.0) {
        return 'medium';
    }
    if (score < 9.0) {
        return 'high';
    }
    return 'critical';
}

export function classifyComplexityScore(value: number): RiskLevel {
    return classifyCvssScore(complexityToCvssScore(value));
}

const RISK_LABEL: Record<RiskLevel, string> = {
    none: 'None',
    low: 'Low',
    medium: 'Medium',
    high: 'High',
    critical: 'Critical'
};

const RISK_DESCRIPTION: Record<RiskLevel, string> = {
    none: 'no violations found',
    low: 'simple, easy to test exhaustively',
    medium: 'getting harder to cover with tests',
    high: 'complex, testing all paths is impractical',
    critical: 'effectively untestable'
};

const CATEGORY_LABEL: Record<string, string> = {
    [VIOLATION_TYPE.NESTING]: 'Nesting depth',
    [VIOLATION_TYPE.COMPLEXITY]: 'Cyclomatic complexity',
    [VIOLATION_TYPE.COGNITIVE]: 'Cognitive complexity',
    [VIOLATION_TYPE.NAMING]: 'Naming',
    [VIOLATION_TYPE.COHERENCE]: 'File coherence',
    [VIOLATION_TYPE.MAGIC]: 'Magic values',
    [VIOLATION_TYPE.PARAMETERS]: 'Parameter count',
    [VIOLATION_TYPE.INVERSION]: 'Inversion opportunities',
    [VIOLATION_TYPE.PRIMITIVE_OBSESSION]: 'Primitive obsession',
    [VIOLATION_TYPE.MATCH_OPPORTUNITY]: 'Match opportunities',
    [VIOLATION_TYPE.LOGICAL_CONTROL_FLOW]: 'Logical operator as control flow',
    [VIOLATION_TYPE.OPAQUE_BOOLEAN]: 'Opaque boolean literals'
};

// decision: only the non-complexity categories get a static blurb — cyclomatic/cognitive
// findings are described from their own extracted numbers instead (see describeCategory)
const CATEGORY_BLURB: Record<string, string> = {
    [VIOLATION_TYPE.NESTING]: 'control-flow blocks nested deep enough that a reader has to hold several levels of context in mind at once',
    [VIOLATION_TYPE.NAMING]: 'naming that obscures intent',
    [VIOLATION_TYPE.COHERENCE]: 'the file mixes too many responsibilities (too many functions/imports, or too many large functions) to read as one coherent unit',
    [VIOLATION_TYPE.MAGIC]: 'unnamed literals standing in for a value that deserves a name',
    [VIOLATION_TYPE.PARAMETERS]: 'a function with enough parameters that call sites are easy to get wrong',
    [VIOLATION_TYPE.INVERSION]: 'validation/guard logic that would read more clearly as early returns',
    [VIOLATION_TYPE.PRIMITIVE_OBSESSION]: 'adjacent same-typed values a caller could silently swap without the compiler noticing',
    [VIOLATION_TYPE.MATCH_OPPORTUNITY]: 'an if/elif chain on one variable that would read more clearly as a match/switch',
    [VIOLATION_TYPE.LOGICAL_CONTROL_FLOW]: '&&/|| used to hide an if statement',
    [VIOLATION_TYPE.OPAQUE_BOOLEAN]: 'a bare true/false at a call site that only makes sense by reading the callee'
};

const COMPLEXITY_VALUE_PATTERN = /complexity: (\d+)/i;

// decision: extracts the numeric complexity from the violation message rather than adding a
// structured field to EnergyViolation — see cyclomatic.ts/cognitive.ts, whose message format
// (`High {cyclomatic|cognitive} complexity: N. ...`) is the only place that number lives today,
// and both messages are only emitted above their own mediumThreshold, so any match is meaningful
function extractComplexityValue(violation: EnergyViolation): number | undefined {
    if (violation.type !== VIOLATION_TYPE.COMPLEXITY && violation.type !== VIOLATION_TYPE.COGNITIVE) {
        return undefined;
    }
    const match = violation.message.match(COMPLEXITY_VALUE_PATTERN);
    return match ? Number(match[1]) : undefined;
}

function describeComplexityFindings(label: string, violations: EnergyViolation[]): string {
    const values = violations
        .map(extractComplexityValue)
        .filter((value): value is number => value !== undefined)
        .sort((a, b) => b - a);

    if (values.length === 0) {
        return '';
    }

    const worst = values[0];
    const cvss = complexityToCvssScore(worst);
    const level = classifyCvssScore(cvss);
    const countText = values.length === 1
        ? `1 function scores ${worst}`
        : `${values.length} functions score ${values.join(', ')} (worst: ${worst})`;

    return `- **${label}**: ${countText} — CVSS ${cvss.toFixed(1)} (${RISK_LABEL[level]}): ${RISK_DESCRIPTION[level]}.`;
}

function describeCategoryFindings(type: string, violations: EnergyViolation[]): string {
    const label = CATEGORY_LABEL[type] ?? type;

    if (type === VIOLATION_TYPE.COMPLEXITY || type === VIOLATION_TYPE.COGNITIVE) {
        return describeComplexityFindings(label, violations);
    }

    const counts = emptyCounts();
    for (const violation of violations) {
        counts[violation.severity] += 1;
    }
    const severityText = (['high', 'medium', 'low'] as const)
        .filter(severity => counts[severity] > 0)
        .map(severity => `${counts[severity]} ${severity}`)
        .join(', ');
    const blurb = CATEGORY_BLURB[type];

    return `- **${label}**: ${violations.length} finding${violations.length === 1 ? '' : 's'} (${severityText})${blurb ? ` — ${blurb}` : ''}.`;
}

// decision: falls back to a fixed severity-based score (High->7.5, Medium->5.0, Low->2.0) when a
// file has no cyclomatic/cognitive violations at all — those detectors don't fire on every file,
// but a file full of high-severity findings from other detectors still deserves a non-zero score.
// invariant: the fallback never reaches Critical (score < 9.0) — Critical is reserved for genuinely
// extreme complexity (>=~93 on the interpolated curve), not for a pattern-based detector's severity
function fileScore(violations: EnergyViolation[]): number {
    const complexityValues = violations
        .map(extractComplexityValue)
        .filter((value): value is number => value !== undefined);

    if (complexityValues.length > 0) {
        return complexityToCvssScore(Math.max(...complexityValues));
    }
    if (violations.some(v => v.severity === SEVERITY.HIGH)) {
        return 7.5;
    }
    if (violations.some(v => v.severity === SEVERITY.MEDIUM)) {
        return 5.0;
    }
    if (violations.length > 0) {
        return 2.0;
    }
    return 0.0;
}

function renderFileSection(result: FileResult): string {
    const lines: string[] = [];
    const score = fileScore(result.violations);
    const risk = classifyCvssScore(score);

    lines.push(`## ${result.filePath} — ${RISK_LABEL[risk]} (CVSS ${score.toFixed(1)})`);
    lines.push('');

    const byType = new Map<string, EnergyViolation[]>();
    for (const violation of result.violations) {
        const list = byType.get(violation.type) ?? [];
        list.push(violation);
        byType.set(violation.type, list);
    }

    for (const [type, violations] of byType) {
        const description = describeCategoryFindings(type, violations);
        if (description) {
            lines.push(description);
        }
    }

    return lines.join('\n');
}

const SCORE_LEGEND = [
    '## Score legend',
    '',
    '_Risk is reported on the CVSS (Common Vulnerability Scoring System) severity scale — the same scale used for CVEs — so the label carries a weight most developers already have an intuition for._',
    '',
    '| CVSS score | Risk | Roughly | Cyclomatic/cognitive complexity |',
    '| --- | --- | --- | --- |',
    '| 0.0 | None | No violations found | — |',
    '| 0.1–3.9 | Low | Simple, easy to test exhaustively | 1–10 |',
    '| 4.0–6.9 | Medium | Getting harder to cover with tests | 11–20 |',
    '| 7.0–8.9 | High | Complex, testing all paths is impractical | 21–50 |',
    '| 9.0–10.0 | Critical | Effectively untestable | 50+ |',
    '',
    '_Cyclomatic and cognitive complexity numbers are converted to CVSS using the ranges above. Other detectors flag a pattern rather than a path count, so a file with no complexity violations of its own instead gets a fixed score from its worst other finding (Low 2.0 / Medium 5.0 / High 7.5)._'
].join('\n');

export function renderHumanReport(results: FileResult[]): string {
    const flagged = results
        .filter(r => r.violations.length > 0)
        .sort((a, b) => fileScore(b.violations) - fileScore(a.violations));
    const cleanCount = results.length - flagged.length;

    const lines: string[] = [];
    lines.push('# Energy State Report');
    lines.push('');
    lines.push(SCORE_LEGEND);
    lines.push('');
    lines.push(`**${results.length} file${results.length === 1 ? '' : 's'} scanned** — ${cleanCount} clean, ${flagged.length} flagged`);
    lines.push('');

    for (const result of flagged) {
        lines.push(renderFileSection(result));
        lines.push('');
    }

    lines.push('## Total evaluation');
    lines.push('');

    const fileScores = results.map(r => ({ filePath: r.filePath, score: fileScore(r.violations) }));
    const worst = fileScores.reduce((a, b) => (b.score > a.score ? b : a), { filePath: '', score: 0 });
    const repoLevel = classifyCvssScore(worst.score);

    if (worst.score > 0) {
        lines.push(`**Repo score: ${worst.score.toFixed(1)} (${RISK_LABEL[repoLevel]})** — driven by the worst file in the scan, \`${worst.filePath}\` (${RISK_DESCRIPTION[repoLevel]}).`);
    } else {
        lines.push('**Repo score: 0.0 (None)** — no violations were found anywhere in the scan.');
    }
    lines.push('');
    lines.push('This is the _maximum_ file score, not an average across files — see the note at the top of this report for why an average would hide the file most worth fixing.');
    lines.push('');

    const riskCounts: Record<RiskLevel, number> = { none: 0, low: 0, medium: 0, high: 0, critical: 0 };
    for (const { score } of fileScores) {
        riskCounts[classifyCvssScore(score)] += 1;
    }

    lines.push('| Risk | Files |');
    lines.push('| --- | --- |');
    lines.push(`| None | ${riskCounts.none} |`);
    lines.push(`| Low | ${riskCounts.low} |`);
    lines.push(`| Medium | ${riskCounts.medium} |`);
    lines.push(`| High | ${riskCounts.high} |`);
    lines.push(`| Critical | ${riskCounts.critical} |`);
    lines.push('');

    const totalCounts = emptyCounts();
    for (const result of results) {
        for (const violation of result.violations) {
            totalCounts[violation.severity] += 1;
        }
    }
    const totalFindings = totalCounts.low + totalCounts.medium + totalCounts.high;
    lines.push(`**${totalFindings} total finding${totalFindings === 1 ? '' : 's'}** (${totalCounts.high} high, ${totalCounts.medium} medium, ${totalCounts.low} low) — breadth of issues across the scan, independent of peak severity.`);

    return lines.join('\n');
}
