import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE, SEVERITY } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: cyclomatic complexity (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/cyclomaticComplexity.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/cyclomaticComplexity.ts'],
        ['F#', FSHARP, 'fsharp/cyclomaticComplexity.fs'],
        ['Kotlin', KOTLIN, 'kotlin/cyclomaticComplexity.kt']
    ] as const) {
        test(`${label}: a single branch stays clean, an 11-way branch is medium, a 16-way branch is high`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanSimpleFunction');
            const complex = findFunctionRange(sourceCode, 'flaggedComplexFunction');
            const severe = findFunctionRange(sourceCode, 'flaggedSevereFunction');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.COMPLEXITY).length, 0,
                'a single if/else should not be flagged');

            const complexHit = violationsIn(violations, complex).filter(v => v.type === VIOLATION_TYPE.COMPLEXITY);
            assert.ok(complexHit.length > 0, 'expected a complexity violation for the 11-branch function');
            assert.strictEqual(complexHit[0].severity, SEVERITY.MEDIUM);

            const severeHit = violationsIn(violations, severe).filter(v => v.type === VIOLATION_TYPE.COMPLEXITY);
            assert.ok(severeHit.length > 0, 'expected a complexity violation for the 16-branch function');
            assert.strictEqual(severeHit[0].severity, SEVERITY.HIGH);
        });
    }
});
