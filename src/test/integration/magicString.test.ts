import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: magic strings (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/magicString.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/magicString.ts'],
        ['F#', FSHARP, 'fsharp/magicString.fs']
    ] as const) {
        test(`${label}: messages/interpolated/single-use strings stay clean, repeated comparisons are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanValues');
            const strings = findFunctionRange(sourceCode, 'flaggedMagicString');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
                'an interpolated string, a logging call argument, and a single-use dict key should not be flagged');

            const stringHits = violationsIn(violations, strings).filter(v => v.type === VIOLATION_TYPE.MAGIC);
            assert.ok(stringHits.length > 0, 'expected a magic-string violation for "pending", compared twice via ==');
        });
    }

    test('magicString.enabled: false suppresses all magic-string violations', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py', {
            magicString: { enabled: false, minDuplicates: 2, allowlist: [] }
        });
        assert.strictEqual(violations.filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'disabling the detector should leave no magic-string violations');
    });

    test('magicString.minDuplicates: 1 flags even single-occurrence decision-point strings', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py', {
            magicString: { enabled: true, minDuplicates: 1, allowlist: ['', 'utf-8', '__main__'] }
        });
        const clean = findFunctionRange(sourceCode, 'cleanValues');
        const hits = violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.MAGIC);
        assert.ok(hits.length > 0, 'lowering minDuplicates to 1 should flag the single-use config["timeout"] key');
    });

    test('magicString.allowlist: a custom allowlist exempts additional values', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py', {
            magicString: { enabled: true, minDuplicates: 2, allowlist: ['', 'utf-8', '__main__', 'pending'] }
        });
        const strings = findFunctionRange(sourceCode, 'flaggedMagicString');
        assert.strictEqual(violationsIn(violations, strings).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'literals added to the allowlist should no longer be flagged');
    });
});
