import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: magic numbers (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/magicNumber.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/magicNumber.ts'],
        ['F#', FSHARP, 'fsharp/magicNumber.fs']
    ] as const) {
        test(`${label}: allowlisted values stay clean, significant literals are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanCommonValues');
            const numbers = findFunctionRange(sourceCode, 'flaggedMagicNumbers');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
                '0 and 1 are on the default allowlist and a named constant binding should not be flagged');

            const numberHits = violationsIn(violations, numbers).filter(v => v.type === VIOLATION_TYPE.MAGIC);
            assert.ok(numberHits.length > 0, 'expected magic-number violations for 50 and 15.75/1.08');
        });
    }

    test('array index and default parameter value are exempt', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicNumber.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicNumber.py');
        const exempt = findFunctionRange(sourceCode, 'exemptIndexAndDefault');
        assert.strictEqual(violationsIn(violations, exempt).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'arr[0] and a default parameter value of 42 should not be flagged');
    });

    test('magicNumber.enabled: false suppresses all magic-number violations', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicNumber.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicNumber.py', {
            magicNumber: { enabled: false, allowlist: [] }
        });
        assert.strictEqual(violations.filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'disabling the detector should leave no magic-number violations');
    });

    test('magicNumber.allowlist: a custom allowlist exempts additional values', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicNumber.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicNumber.py', {
            magicNumber: { enabled: true, allowlist: [0, 1, -1, 2, 50, 15.75, 1.08] }
        });
        const numbers = findFunctionRange(sourceCode, 'flaggedMagicNumbers');
        assert.strictEqual(violationsIn(violations, numbers).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'literals added to the allowlist should no longer be flagged');
    });
});
