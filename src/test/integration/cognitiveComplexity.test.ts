import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE, SEVERITY } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: cognitive complexity (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/cognitiveComplexity.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/cognitiveComplexity.ts'],
        ['F#', FSHARP, 'fsharp/cognitiveComplexity.fs'],
        ['Kotlin', KOTLIN, 'kotlin/cognitiveComplexity.kt']
    ] as const) {
        test(`${label}: a flat check stays clean, 6-deep nesting is medium, 7-deep nesting is high`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanSimpleFunction');
            const complex = findFunctionRange(sourceCode, 'flaggedComplexFunction');
            const severe = findFunctionRange(sourceCode, 'flaggedSevereFunction');

            assert.strictEqual(
                violationsIn(violations, clean).filter((v) => v.type === VIOLATION_TYPE.COGNITIVE).length,
                0,
                'a single flat if should not be flagged'
            );

            const complexHit = violationsIn(violations, complex).filter((v) => v.type === VIOLATION_TYPE.COGNITIVE);
            assert.ok(complexHit.length > 0, 'expected a cognitive-complexity violation for the 6-deep function');
            assert.strictEqual(complexHit[0].severity, SEVERITY.MEDIUM);

            const severeHit = violationsIn(violations, severe).filter((v) => v.type === VIOLATION_TYPE.COGNITIVE);
            assert.ok(severeHit.length > 0, 'expected a cognitive-complexity violation for the 7-deep function');
            assert.strictEqual(severeHit[0].severity, SEVERITY.HIGH);
        });
    }
});
