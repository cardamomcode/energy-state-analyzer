import * as assert from 'assert';

import { classifyComplexityScore, classifyCvssScore, complexityToCvssScore, diffSummaries, renderDiffMarkdown, renderHumanReport, renderMarkdownReport, summarize, summarizeFile, FileResult } from '../../core/report';
import { EnergyViolation, SEVERITY, VIOLATION_TYPE } from '../../types';

function violation(severity: 'low' | 'medium' | 'high', type: EnergyViolation['type'] = VIOLATION_TYPE.COMPLEXITY, message = 'test'): EnergyViolation {
    return { line: 0, column: 0, type, severity, message };
}

function cyclomatic(value: number, severity: 'medium' | 'high' = 'medium') {
    return violation(severity, VIOLATION_TYPE.COMPLEXITY, `High cyclomatic complexity: ${value}. Consider breaking down this function.`);
}

function cognitive(value: number, severity: 'medium' | 'high' = 'medium') {
    return violation(severity, VIOLATION_TYPE.COGNITIVE, `High cognitive complexity: ${value}. This function is hard to read.`);
}

suite('Integration: report (summarize/diff/render)', () => {
    test('summarizeFile scores by severity weight (1/4/9) and tallies counts/types', () => {
        const result: FileResult = {
            filePath: 'a.py',
            violations: [violation(SEVERITY.LOW), violation(SEVERITY.MEDIUM), violation(SEVERITY.HIGH), violation(SEVERITY.HIGH)]
        };

        const summary = summarizeFile(result);

        assert.strictEqual(summary.score, 1 + 4 + 9 + 9);
        assert.deepStrictEqual(summary.counts, { low: 1, medium: 1, high: 2 });
        assert.strictEqual(summary.byType[VIOLATION_TYPE.COMPLEXITY], 4);
    });

    test('summarizeFile scores a clean file as zero', () => {
        const summary = summarizeFile({ filePath: 'clean.py', violations: [] });
        assert.strictEqual(summary.score, 0);
        assert.deepStrictEqual(summary.counts, { low: 0, medium: 0, high: 0 });
    });

    test('summarize aggregates totals across files', () => {
        const summary = summarize([
            { filePath: 'a.py', violations: [violation(SEVERITY.HIGH)] },
            { filePath: 'b.py', violations: [violation(SEVERITY.MEDIUM), violation(SEVERITY.LOW)] }
        ]);

        assert.strictEqual(summary.totalScore, 9 + 4 + 1);
        assert.deepStrictEqual(summary.totalCounts, { low: 1, medium: 1, high: 1 });
        assert.strictEqual(summary.files.length, 2);
    });

    test('renderMarkdownReport includes a table row per file and a total line', () => {
        const summary = summarize([
            { filePath: 'a.py', violations: [violation(SEVERITY.HIGH)] },
            { filePath: 'clean.py', violations: [] }
        ]);

        const markdown = renderMarkdownReport(summary);

        assert.ok(markdown.includes('| a.py | 9 | 1 | 0 | 0 |'));
        assert.ok(markdown.includes('| clean.py | 0 | 0 | 0 | 0 |'));
        assert.ok(markdown.includes('1 clean, 1 with violations'));
        assert.ok(markdown.includes('**Total score: 9**'));
    });

    test('diffSummaries flags improved, worsened, unchanged, and new files', () => {
        const base = [
            { filePath: 'worse.py', score: 0, counts: { low: 0, medium: 0, high: 0 }, byType: {} },
            { filePath: 'better.py', score: 9, counts: { low: 0, medium: 0, high: 1 }, byType: {} },
            { filePath: 'same.py', score: 4, counts: { low: 0, medium: 1, high: 0 }, byType: {} }
        ];
        const head = [
            { filePath: 'worse.py', score: 9, counts: { low: 0, medium: 0, high: 1 }, byType: {} },
            { filePath: 'better.py', score: 0, counts: { low: 0, medium: 0, high: 0 }, byType: {} },
            { filePath: 'same.py', score: 4, counts: { low: 0, medium: 1, high: 0 }, byType: {} },
            { filePath: 'new.py', score: 1, counts: { low: 1, medium: 0, high: 0 }, byType: {} }
        ];

        const entries = diffSummaries(base, head);
        const byPath = new Map(entries.map(e => [e.filePath, e]));

        assert.strictEqual(byPath.get('worse.py')?.status, 'worsened');
        assert.strictEqual(byPath.get('worse.py')?.delta, 9);
        assert.strictEqual(byPath.get('better.py')?.status, 'improved');
        assert.strictEqual(byPath.get('better.py')?.delta, -9);
        assert.strictEqual(byPath.get('same.py')?.status, 'unchanged');
        assert.strictEqual(byPath.get('same.py')?.delta, 0);
        assert.strictEqual(byPath.get('new.py')?.status, 'new');
        assert.strictEqual(byPath.get('new.py')?.baseScore, null);
    });

    test('renderDiffMarkdown renders base ref, per-file deltas, and a summary line', () => {
        const entries = diffSummaries(
            [{ filePath: 'a.py', score: 0, counts: { low: 0, medium: 0, high: 0 }, byType: {} }],
            [
                { filePath: 'a.py', score: 4, counts: { low: 0, medium: 1, high: 0 }, byType: {} },
                { filePath: 'b.py', score: 1, counts: { low: 1, medium: 0, high: 0 }, byType: {} }
            ]
        );

        const markdown = renderDiffMarkdown(entries, 'origin/main');

        assert.ok(markdown.includes('vs `origin/main`'));
        assert.ok(markdown.includes('| a.py | 0 | 4 | +4 | 🔴 worsened |'));
        assert.ok(markdown.includes('| b.py | — | 1 | — | 🆕 new |'));
        assert.ok(markdown.includes('2 files changed, 1 worsened, 0 improved, 1 new.'));
    });
});

suite('Integration: report (CVSS mapping)', () => {
    test('complexityToCvssScore interpolates between the README McCabe breakpoints (10/20/50/100)', () => {
        assert.strictEqual(complexityToCvssScore(0), 0);
        assert.strictEqual(complexityToCvssScore(10), 3.9);
        assert.strictEqual(complexityToCvssScore(20), 6.9);
        assert.strictEqual(complexityToCvssScore(50), 8.9);
        assert.strictEqual(complexityToCvssScore(100), 10.0);
        assert.strictEqual(complexityToCvssScore(200), 10.0, 'caps at 10.0 beyond the curve');
    });

    test('classifyCvssScore matches the official CVSS v3.1 qualitative severity boundaries', () => {
        assert.strictEqual(classifyCvssScore(0), 'none');
        assert.strictEqual(classifyCvssScore(0.1), 'low');
        assert.strictEqual(classifyCvssScore(3.9), 'low');
        assert.strictEqual(classifyCvssScore(4.0), 'medium');
        assert.strictEqual(classifyCvssScore(6.9), 'medium');
        assert.strictEqual(classifyCvssScore(7.0), 'high');
        assert.strictEqual(classifyCvssScore(8.9), 'high');
        assert.strictEqual(classifyCvssScore(9.0), 'critical');
        assert.strictEqual(classifyCvssScore(10.0), 'critical');
    });

    test('classifyComplexityScore composes the two — a complexity of 34 is High, 60 is Critical', () => {
        assert.strictEqual(classifyComplexityScore(34), 'high');
        assert.strictEqual(classifyComplexityScore(60), 'critical');
    });
});

suite('Integration: report (renderHumanReport)', () => {
    test('describes a cyclomatic/cognitive finding with its CVSS-equivalent score and risk label', () => {
        const markdown = renderHumanReport([{ filePath: 'a.py', violations: [cyclomatic(34, 'high')] }]);

        assert.ok(markdown.includes('## a.py — High (CVSS 7.8)'));
        assert.ok(markdown.includes('**Cyclomatic complexity**: 1 function scores 34 — CVSS 7.8 (High): complex, testing all paths is impractical.'));
    });

    test('lists multiple complexity values worst-first and reports the worst in the CVSS score/label', () => {
        const markdown = renderHumanReport([{ filePath: 'a.py', violations: [cognitive(12), cognitive(60, 'high')] }]);

        assert.ok(markdown.includes('**Cognitive complexity**: 2 functions score 60, 12 (worst: 60) — CVSS 9.1 (Critical): effectively untestable.'));
        assert.ok(markdown.includes('## a.py — Critical (CVSS 9.1)'));
    });

    test('describes a non-complexity category by finding count and severity, with a static blurb', () => {
        const markdown = renderHumanReport([{ filePath: 'a.py', violations: [violation('medium', VIOLATION_TYPE.PRIMITIVE_OBSESSION), violation('low', VIOLATION_TYPE.PRIMITIVE_OBSESSION)] }]);

        assert.ok(markdown.includes('**Primitive obsession**: 2 findings (1 medium, 1 low) — adjacent same-typed values a caller could silently swap without the compiler noticing.'));
    });

    test('falls back to a fixed severity-based score when there are no complexity violations', () => {
        const markdown = renderHumanReport([{ filePath: 'a.py', violations: [violation('high', VIOLATION_TYPE.COHERENCE)] }]);
        assert.ok(markdown.includes('## a.py — High (CVSS 7.5)'));
    });

    test('omits clean files from the per-file sections but counts them in the summary line', () => {
        const markdown = renderHumanReport([
            { filePath: 'a.py', violations: [cyclomatic(15)] },
            { filePath: 'clean.py', violations: [] }
        ]);

        assert.ok(markdown.includes('2 files scanned** — 1 clean, 1 flagged'));
        assert.ok(!markdown.includes('## clean.py'));
    });

    test('lists flagged files worst-first', () => {
        const markdown = renderHumanReport([
            { filePath: 'mild.py', violations: [cyclomatic(15)] },
            { filePath: 'severe.py', violations: [cyclomatic(60, 'high')] }
        ]);

        assert.ok(markdown.indexOf('## severe.py') < markdown.indexOf('## mild.py'), 'the worse file should be listed first');
    });

    test('repo score is the maximum file score, not an average across files', () => {
        const markdown = renderHumanReport([
            { filePath: 'many-trivial.py', violations: [cyclomatic(2), cyclomatic(2), cyclomatic(2)] },
            { filePath: 'one-severe.py', violations: [cyclomatic(60, 'high')] }
        ]);

        assert.ok(markdown.includes('**Repo score: 9.1 (Critical)** — driven by the worst file in the scan, `one-severe.py`'));
        assert.ok(!markdown.includes('Repo score: 0.8'), 'many-trivial.py alone would score 0.8 (Low) — the worse file must win, not an average of the two');
    });

    test('repo score falls back to a Low fixed score when no complexity violations exist anywhere', () => {
        const markdown = renderHumanReport([{ filePath: 'a.py', violations: [violation('low', VIOLATION_TYPE.MAGIC)] }]);
        assert.ok(markdown.includes('**Repo score: 2.0 (Low)** — driven by the worst file in the scan, `a.py`'));
    });

    test('repo score is 0.0 (None) when the whole scan is clean', () => {
        const markdown = renderHumanReport([{ filePath: 'clean.py', violations: [] }]);
        assert.ok(markdown.includes('**Repo score: 0.0 (None)** — no violations were found anywhere in the scan.'));
    });

    test('total evaluation tallies files by CVSS risk band and total findings by severity', () => {
        const markdown = renderHumanReport([
            { filePath: 'low.py', violations: [violation('low', VIOLATION_TYPE.MAGIC)] },
            { filePath: 'high.py', violations: [cyclomatic(30, 'high')] },
            { filePath: 'clean.py', violations: [] }
        ]);

        assert.ok(markdown.includes('| None | 1 |'));
        assert.ok(markdown.includes('| Low | 1 |'));
        assert.ok(markdown.includes('| High | 1 |'));
        assert.ok(markdown.includes('**2 total findings** (1 high, 0 medium, 1 low)'));
    });
});
