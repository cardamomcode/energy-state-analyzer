import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: magic values (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/magicValues.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/magicValues.ts'],
        ['F#', FSHARP, 'fsharp/magicValues.fs']
    ] as const) {
        test(`${label}: common/named values stay clean, bare literals and message strings are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanCommonValues');
            const numbers = findFunctionRange(sourceCode, 'flaggedMagicNumbers');
            const strings = findFunctionRange(sourceCode, 'flaggedMagicString');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
                '0, 1, 100 and a named constant binding should not be flagged');

            const numberHits = violationsIn(violations, numbers).filter(v => v.type === VIOLATION_TYPE.MAGIC);
            assert.ok(numberHits.length > 0, 'expected magic-number violations for 50 and 15.75');

            const stringHits = violationsIn(violations, strings).filter(v => v.type === VIOLATION_TYPE.MAGIC);
            assert.ok(stringHits.length > 0, 'expected a magic-string violation for the error-shaped message literal');
        });
    }

    test('magicValues.enabled: false suppresses all magic-value violations', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicValues.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicValues.py', {
            magicValues: { enabled: false }
        });
        assert.strictEqual(violations.filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'disabling the detector should leave no magic-value violations');
    });
});
