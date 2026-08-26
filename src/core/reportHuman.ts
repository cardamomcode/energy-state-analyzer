// decision: split out of report.ts (the human-readable risk report) so the aggregate-scoring,
// diff, and human-report domains each live in their own file — see the coherence note in
// report.ts
//
// decision: risk is reported on a None/Low/Medium/High/Critical qualitative scale rather than
// a bespoke label set of our own — it mirrors the levels the existing SEVERITY constants already
// use, just extended to five tiers, so a reader doesn't have to learn a new vocabulary. Earlier
// this scale was explicitly framed as CVSS (the vulnerability-scoring standard); that framing was
// dropped because it made readers think "why is my code getting a CVE score", which is the
// opposite of a scale they should already be able to reason about.
// decision: the underlying 0.0-10.0 complexity score is a piecewise-linear mapping of
// the raw complexity number, anchored at README's existing McCabe breakpoints (10/20/50) —
// see complexityToScore below — so the label is a re-expression of the same,
// already-documented complexity bands rather than a second, independently-calibrated scale.
// decision: a file's/report's headline risk is the MAXIMUM complexity value found, not an
// average — averaging a file's function complexities would let one severely complex function
// hide behind many trivial ones (nine functions at complexity 2 and one at 60 averages to ~8,
// "Low"), masking exactly the function worth fixing first. Violation *counts* are reported
// separately as a breadth indicator, deliberately not folded into the same number — this
// mirrors the README's own Energy (peak intensity) vs Entropy (how spread out) distinction.
import { EnergyViolation, SEVERITY, VIOLATION_TYPE } from '../types';
import { emptyCounts } from './severityCounts';
import type { FileResult } from './report';

export type RiskLevel = 'none' | 'low' | 'medium' | 'high' | 'critical';

// decision: anchors (value, score) pairs at the exact breakpoints README already documents
// for cyclomatic complexity (10/20/50), reusing CVSS v3.1's qualitative boundaries
// (3.9/6.9/8.9/10.0) as the corresponding output scores purely as a convenient 0-10 curve
// shape — no CVSS terminology is surfaced to the reader — then linearly interpolates between
// anchors and caps at complexity 100+ = 10.0
// decision: the anchor scores are deliberately 0.1 below classifyScore's cutoffs (3.9 not 4.0,
// 6.9 not 7.0, 8.9 not 9.0), not rounded to whole numbers — a complexity of exactly 10/20/50 is
// the top of its documented band (10 = top of Low, per README), so its score must land just
// under the next band's cutoff to classify correctly. Rounding these to 4.0/7.0/9.0 would push
// complexity exactly 10/20/50 into the next band up, silently relabeling the documented
// breakpoints themselves.
const COMPLEXITY_CURVE: { value: number; score: number }[] = [
    { value: 0, score: 0.0 },
    { value: 10, score: 3.9 },
    { value: 20, score: 6.9 },
    { value: 50, score: 8.9 },
    { value: 100, score: 10.0 }
];

// decision: derived from COMPLEXITY_CURVE's own last anchor rather than re-hardcoded — the cap
// this function returns for out-of-range input must always agree with the curve it's capping
const MAX_COMPLEXITY_VALUE = COMPLEXITY_CURVE[COMPLEXITY_CURVE.length - 1].value;
const MAX_COMPLEXITY_SCORE = COMPLEXITY_CURVE[COMPLEXITY_CURVE.length - 1].score;
const SCORE_DECIMAL_PRECISION = 10;

export function complexityToScore(value: number): number {
    if (value <= 0) {
        return 0;
    }
    if (value >= MAX_COMPLEXITY_VALUE) {
        return MAX_COMPLEXITY_SCORE;
    }
    for (let i = 1; i < COMPLEXITY_CURVE.length; i++) {
        const prev = COMPLEXITY_CURVE[i - 1];
        const next = COMPLEXITY_CURVE[i];
        if (value <= next.value) {
            const ratio = (value - prev.value) / (next.value - prev.value);
            return (
                Math.round((prev.score + ratio * (next.score - prev.score)) * SCORE_DECIMAL_PRECISION) /
                SCORE_DECIMAL_PRECISION
            );
        }
    }
    return MAX_COMPLEXITY_SCORE;
}

// decision: boundaries reuse CVSS v3.1's qualitative rating cutoffs as a scale shape
// (None 0.0, Low 0.1-3.9, Medium 4.0-6.9, High 7.0-8.9, Critical 9.0-10.0) without labeling
// the scale itself as CVSS anywhere the reader sees it
const RISK_SCORE_CUTOFF: Record<'low' | 'medium' | 'high', number> = {
    low: 4.0,
    medium: 7.0,
    high: 9.0
};

export function classifyScore(score: number): RiskLevel {
    if (score <= 0) {
        return 'none';
    }
    if (score < RISK_SCORE_CUTOFF.low) {
        return 'low';
    }
    if (score < RISK_SCORE_CUTOFF.medium) {
        return 'medium';
    }
    if (score < RISK_SCORE_CUTOFF.high) {
        return 'high';
    }
    return 'critical';
}

export function classifyComplexityScore(value: number): RiskLevel {
    return classifyScore(complexityToScore(value));
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
    [VIOLATION_TYPE.NESTING]:
        'control-flow blocks nested deep enough that a reader has to hold several levels of context in mind at once',
    [VIOLATION_TYPE.NAMING]: 'naming that obscures intent',
    [VIOLATION_TYPE.COHERENCE]:
        'the file mixes too many responsibilities (too many functions/imports, or too many large functions) to read as one coherent unit',
    [VIOLATION_TYPE.MAGIC]: 'unnamed literals standing in for a value that deserves a name',
    [VIOLATION_TYPE.PARAMETERS]: 'a function with enough parameters that call sites are easy to get wrong',
    [VIOLATION_TYPE.INVERSION]: 'validation/guard logic that would read more clearly as early returns',
    [VIOLATION_TYPE.PRIMITIVE_OBSESSION]:
        'adjacent same-typed values a caller could silently swap without the compiler noticing',
    [VIOLATION_TYPE.MATCH_OPPORTUNITY]:
        'an if/elif chain on one variable that would read more clearly as a match/switch',
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
    const score = complexityToScore(worst);
    const level = classifyScore(score);
    const countText =
        values.length === 1
            ? `1 function scores ${worst}`
            : `${values.length} functions score ${values.join(', ')} (worst: ${worst})`;

    return `- **${label}**: ${countText} — score ${score.toFixed(1)} (${RISK_LABEL[level]}): ${RISK_DESCRIPTION[level]}.`;
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
        .filter((severity) => counts[severity] > 0)
        .map((severity) => `${counts[severity]} ${severity}`)
        .join(', ');
    const blurb = CATEGORY_BLURB[type];

    return `- **${label}**: ${violations.length} finding${violations.length === 1 ? '' : 's'} (${severityText})${blurb ? ` — ${blurb}` : ''}.`;
}

// decision: falls back to a fixed severity-based score when a file has no cyclomatic/cognitive
// violations at all — those detectors don't fire on every file, but a file full of high-severity
// findings from other detectors still deserves a non-zero score.
// invariant: the fallback never reaches Critical (score < 9.0) — Critical is reserved for genuinely
// extreme complexity (>=~93 on the interpolated curve), not for a pattern-based detector's severity
const FALLBACK_SEVERITY_SCORE: Record<'high' | 'medium' | 'low', number> = {
    high: 7.5,
    medium: 5.0,
    low: 2.0
};

function fileScore(violations: EnergyViolation[]): number {
    const complexityValues = violations
        .map(extractComplexityValue)
        .filter((value): value is number => value !== undefined);

    if (complexityValues.length > 0) {
        return complexityToScore(Math.max(...complexityValues));
    }
    if (violations.some((v) => v.severity === SEVERITY.HIGH)) {
        return FALLBACK_SEVERITY_SCORE.high;
    }
    if (violations.some((v) => v.severity === SEVERITY.MEDIUM)) {
        return FALLBACK_SEVERITY_SCORE.medium;
    }
    if (violations.length > 0) {
        return FALLBACK_SEVERITY_SCORE.low;
    }
    return 0.0;
}

function renderFileSection(result: FileResult): string {
    const lines: string[] = [];
    const score = fileScore(result.violations);
    const risk = classifyScore(score);

    lines.push(`## ${result.filePath} — ${RISK_LABEL[risk]} (score ${score.toFixed(1)})`);
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
    '_Risk is reported on a 0.0–10.0 complexity score, sorted into the same None/Low/Medium/High/Critical levels already used elsewhere in this tool._',
    '',
    '| Score | Risk | Roughly | Cyclomatic/cognitive complexity |',
    '| --- | --- | --- | --- |',
    '| 0.0 | None | No violations found | — |',
    '| 0.1–3.9 | Low | Simple, easy to test exhaustively | 1–10 |',
    '| 4.0–6.9 | Medium | Getting harder to cover with tests | 11–20 |',
    '| 7.0–8.9 | High | Complex, testing all paths is impractical | 21–50 |',
    '| 9.0–10.0 | Critical | Effectively untestable | 50+ |',
    '',
    '_Cyclomatic and cognitive complexity numbers are converted to the score using the ranges above. Other detectors flag a pattern rather than a path count, so a file with no complexity violations of its own instead gets a fixed score from its worst other finding (Low 2.0 / Medium 5.0 / High 7.5)._'
].join('\n');

export function renderHumanReport(results: FileResult[]): string {
    const flagged = results
        .filter((r) => r.violations.length > 0)
        .sort((a, b) => fileScore(b.violations) - fileScore(a.violations));
    const cleanCount = results.length - flagged.length;

    const lines: string[] = [];
    lines.push('# Energy State Report');
    lines.push('');
    lines.push(SCORE_LEGEND);
    lines.push('');
    lines.push(
        `**${results.length} file${results.length === 1 ? '' : 's'} scanned** — ${cleanCount} clean, ${flagged.length} flagged`
    );
    lines.push('');

    for (const result of flagged) {
        lines.push(renderFileSection(result));
        lines.push('');
    }

    lines.push('## Total evaluation');
    lines.push('');

    const fileScores = results.map((r) => ({ filePath: r.filePath, score: fileScore(r.violations) }));
    const worst = fileScores.reduce((a, b) => (b.score > a.score ? b : a), { filePath: '', score: 0 });
    const repoLevel = classifyScore(worst.score);

    if (worst.score > 0) {
        lines.push(
            `**Repo score: ${worst.score.toFixed(1)} (${RISK_LABEL[repoLevel]})** — driven by the worst file in the scan, \`${worst.filePath}\` (${RISK_DESCRIPTION[repoLevel]}).`
        );
    } else {
        lines.push('**Repo score: 0.0 (None)** — no violations were found anywhere in the scan.');
    }
    lines.push('');
    lines.push(
        'This is the _maximum_ file score, not an average across files — see the note at the top of this report for why an average would hide the file most worth fixing.'
    );
    lines.push('');

    const riskCounts: Record<RiskLevel, number> = { none: 0, low: 0, medium: 0, high: 0, critical: 0 };
    for (const { score } of fileScores) {
        riskCounts[classifyScore(score)] += 1;
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
    lines.push(
        `**${totalFindings} total finding${totalFindings === 1 ? '' : 's'}** (${totalCounts.high} high, ${totalCounts.medium} medium, ${totalCounts.low} low) — breadth of issues across the scan, independent of peak severity.`
    );

    return lines.join('\n');
}
