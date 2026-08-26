import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE, SEVERITY } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: parameter count (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/parameterCount.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/parameterCount.ts'],
        ['F#', FSHARP, 'fsharp/parameterCount.fs'],
        ['Kotlin', KOTLIN, 'kotlin/parameterCount.kt']
    ] as const) {
        test(`${label}: 2 params stays clean, 6 params is medium, 9 params is high`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanFewParams');
            const many = findFunctionRange(sourceCode, 'flaggedManyParams');
            const tooMany = findFunctionRange(sourceCode, 'flaggedTooManyParams');

            assert.strictEqual(
                violationsIn(violations, clean).filter((v) => v.type === VIOLATION_TYPE.PARAMETERS).length,
                0,
                '2 parameters should not be flagged'
            );

            const manyHit = violationsIn(violations, many).filter((v) => v.type === VIOLATION_TYPE.PARAMETERS);
            assert.ok(manyHit.length > 0, 'expected a parameter-explosion violation for 6 parameters');
            assert.strictEqual(manyHit[0].severity, SEVERITY.MEDIUM);

            const tooManyHit = violationsIn(violations, tooMany).filter((v) => v.type === VIOLATION_TYPE.PARAMETERS);
            assert.ok(tooManyHit.length > 0, 'expected a parameter-explosion violation for 9 parameters');
            assert.strictEqual(tooManyHit[0].severity, SEVERITY.HIGH);
        });
    }
});
