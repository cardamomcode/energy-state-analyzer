import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: opaque boolean literal (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/opaqueBoolean.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/opaqueBoolean.ts'],
        ['F#', FSHARP, 'fsharp/opaqueBoolean.fs']
    ] as const) {
        test(`${label}: a bare boolean passed positionally is flagged; a labeled or non-call boolean is not`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const single = findFunctionRange(sourceCode, 'flaggedPositionalBoolean');
            const singleHit = violationsIn(violations, single).filter(v => v.type === VIOLATION_TYPE.OPAQUE_BOOLEAN);
            assert.strictEqual(singleHit.length, 1, 'a single positional boolean argument should be flagged once');

            const amongOthers = findFunctionRange(sourceCode, 'flaggedPositionalBooleanAmongOthers');
            const amongOthersHit = violationsIn(violations, amongOthers).filter(v => v.type === VIOLATION_TYPE.OPAQUE_BOOLEAN);
            assert.strictEqual(amongOthersHit.length, 1, 'a positional boolean alongside a non-boolean argument should still be flagged');

            const labeledFunctionName = label === 'TypeScript'
                ? 'suppressedObjectLiteralField'
                : label === 'Python'
                    ? 'suppressedKeywordArgument'
                    : 'suppressedNamedArgument';
            const suppressedLabeled = findFunctionRange(sourceCode, labeledFunctionName);
            assert.strictEqual(
                violationsIn(violations, suppressedLabeled).filter(v => v.type === VIOLATION_TYPE.OPAQUE_BOOLEAN).length, 0,
                'a boolean labeled at the call site should not be flagged'
            );

            const nonCall = findFunctionRange(sourceCode, 'suppressedNonCallUsage');
            assert.strictEqual(
                violationsIn(violations, nonCall).filter(v => v.type === VIOLATION_TYPE.OPAQUE_BOOLEAN).length, 0,
                'a boolean that is never a call argument should not be flagged'
            );
        });
    }
});
