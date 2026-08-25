import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE, SEVERITY } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

// decision: runs the full detector pipeline (analyzeSource, the same entry point the
// CLI and the extension use) against realistic multi-function files, rather than
// calling analyzeNesting in isolation - the unit suite in extension.test.ts already
// covers the detector's internals with single-line synthetic snippets.
suite('Integration: nesting (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/nesting.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/nesting.ts'],
        ['F#', FSHARP, 'fsharp/nesting.fs'],
        ['Kotlin', KOTLIN, 'kotlin/nesting.kt']
    ] as const) {
        test(`${label}: shallow nesting stays clean, deep nesting is flagged medium, severe nesting is flagged high`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanShallowNesting');
            const deep = findFunctionRange(sourceCode, 'flaggedDeepNesting');
            const severe = findFunctionRange(sourceCode, 'flaggedSevereNesting');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.NESTING).length, 0,
                'shallow (2-level) nesting should not be flagged');

            const deepNesting = violationsIn(violations, deep).filter(v => v.type === VIOLATION_TYPE.NESTING);
            assert.ok(deepNesting.length > 0, 'expected a nesting violation for the 5-level-deep function');
            assert.strictEqual(deepNesting[0].severity, SEVERITY.MEDIUM);

            const severeNesting = violationsIn(violations, severe).filter(v => v.type === VIOLATION_TYPE.NESTING);
            assert.ok(severeNesting.length > 0, 'expected a nesting violation for the 7-level-deep function');
            assert.strictEqual(severeNesting[severeNesting.length - 1].severity, SEVERITY.HIGH);
        });
    }
});
