import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: magic strings (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/magicString.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/magicString.ts'],
        ['F#', FSHARP, 'fsharp/magicString.fs'],
        ['Kotlin', KOTLIN, 'kotlin/magicString.kt']
    ] as const) {
        test(`${label}: messages/interpolated/single-use strings stay clean, repeated comparisons are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanValues');
            const strings = findFunctionRange(sourceCode, 'flaggedMagicString');

            assert.strictEqual(
                violationsIn(violations, clean).filter((v) => v.type === VIOLATION_TYPE.MAGIC).length,
                0,
                'an interpolated string, a logging call argument, and a single-use dict key should not be flagged'
            );

            // decision: pins the exact count, not just ">0" — "pending" recurs twice but is one
            // distinct literal, so this must be a single grouped violation, not two
            const stringHits = violationsIn(violations, strings).filter((v) => v.type === VIOLATION_TYPE.MAGIC);
            assert.strictEqual(
                stringHits.length,
                1,
                'expected exactly one violation for "pending", compared twice via =='
            );
        });
    }

    test('Python: a variable in (...) membership check is flagged once it recurs, not on a single-use value', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py');
        const membership = findFunctionRange(sourceCode, 'flaggedMembership');
        const hits = violationsIn(violations, membership).filter((v) => v.type === VIOLATION_TYPE.MAGIC);
        assert.strictEqual(hits.length, 1, '"queued" recurs across both `in` checks and should be flagged once');
        assert.ok(
            hits.every((v) => v.message.includes('queued')),
            'the flagged literal should be "queued", not "completed"/"failed" (each single-use)'
        );
    });

    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/magicString.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/magicString.ts'],
        ['Kotlin', KOTLIN, 'kotlin/magicString.kt']
    ] as const) {
        test(`${label}: a dict/object key repeated across variables is flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            const dictKey = findFunctionRange(sourceCode, 'flaggedDictKey');
            const hits = violationsIn(violations, dictKey).filter((v) => v.type === VIOLATION_TYPE.MAGIC);
            assert.strictEqual(
                hits.length,
                1,
                '"timeout" is used as a key on two different objects and should be flagged once'
            );
        });
    }

    test('Python: an f-string used as a dict key is exempt (interpolation is itself evidence it is not a magic value)', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py', {
            magicString: { enabled: true, minDuplicates: 1, allowlist: [] }
        });
        const interpolatedKey = findFunctionRange(sourceCode, 'cleanInterpolatedKey');
        assert.strictEqual(
            violationsIn(violations, interpolatedKey).filter((v) => v.type === VIOLATION_TYPE.MAGIC).length,
            0,
            'an f-string used as config[f"{key}_value"] should stay exempt even with minDuplicates lowered to 1'
        );
    });

    test('Kotlin: a string template used as a dict key is exempt (interpolation is itself evidence it is not a magic value)', async () => {
        const { sourceCode, tree } = await parseFixture(KOTLIN, 'kotlin/magicString.kt');
        const violations = analyzeSource(sourceCode, tree, KOTLIN, 'magicString.kt', {
            magicString: { enabled: true, minDuplicates: 1, allowlist: [] }
        });
        const interpolatedKey = findFunctionRange(sourceCode, 'cleanInterpolatedKey');
        assert.strictEqual(
            violationsIn(violations, interpolatedKey).filter((v) => v.type === VIOLATION_TYPE.MAGIC).length,
            0,
            'a string template used as config["${key}_value"] should stay exempt even with minDuplicates lowered to 1'
        );
    });

    test('magicString.enabled: false suppresses all magic-string violations', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py', {
            magicString: { enabled: false, minDuplicates: 2, allowlist: [] }
        });
        assert.strictEqual(
            violations.filter((v) => v.type === VIOLATION_TYPE.MAGIC).length,
            0,
            'disabling the detector should leave no magic-string violations'
        );
    });

    test('magicString.minDuplicates: 1 flags even single-occurrence decision-point strings', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py', {
            magicString: { enabled: true, minDuplicates: 1, allowlist: ['', 'utf-8', '__main__'] }
        });
        const clean = findFunctionRange(sourceCode, 'cleanValues');
        const hits = violationsIn(violations, clean).filter((v) => v.type === VIOLATION_TYPE.MAGIC);
        assert.ok(hits.length > 0, 'lowering minDuplicates to 1 should flag the single-use config["timeout"] key');
    });

    test('magicString.allowlist: a custom allowlist exempts additional values', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicString.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicString.py', {
            magicString: { enabled: true, minDuplicates: 2, allowlist: ['', 'utf-8', '__main__', 'pending'] }
        });
        const strings = findFunctionRange(sourceCode, 'flaggedMagicString');
        assert.strictEqual(
            violationsIn(violations, strings).filter((v) => v.type === VIOLATION_TYPE.MAGIC).length,
            0,
            'literals added to the allowlist should no longer be flagged'
        );
    });
});
