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
        ['TypeScript', TYPESCRIPT, 'typescript/magicValues.ts']
    ] as const) {
        test(`${label}: common/named values stay clean, bare literals and message strings are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanCommonValues');
            const numbers = findFunctionRange(sourceCode, 'flaggedMagicNumbers');
            const strings = findFunctionRange(sourceCode, 'flaggedMagicString');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
                '0, 1, 100 and a named module-level constant should not be flagged');

            const numberHits = violationsIn(violations, numbers).filter(v => v.type === VIOLATION_TYPE.MAGIC);
            assert.ok(numberHits.length > 0, 'expected magic-number violations for 50 and 15.75');

            const stringHits = violationsIn(violations, strings).filter(v => v.type === VIOLATION_TYPE.MAGIC);
            assert.ok(stringHits.length > 0, 'expected a magic-string violation for the error-shaped message literal');
        });
    }

    // decision: documents a known adapter limitation (see fsharp.ts) rather than
    // asserting the "expected" behavior - F#'s function_or_value_defn -> declaration_expression
    // shape makes every literal inside a top-level `let` binding look like it's in
    // module-level constant context, so isInConstantContext exempts it. This locks in
    // that current behavior as a regression test; if the adapter is ever taught to tell
    // "let NAME = <literal>" apart from "let NAME = <expression containing a literal>",
    // this test should start failing and can be flipped to expect a violation.
    test('F#: magic numbers inside a let-bound function are not detected (documented limitation)', async () => {
        const { sourceCode, tree } = await parseFixture(FSHARP, 'fsharp/magicValues.fs');
        const violations = analyzeSource(sourceCode, tree, FSHARP, 'magicValues.fs');
        assertValidPositions(violations, sourceCode);

        const magicHits = violations.filter(v => v.type === VIOLATION_TYPE.MAGIC);
        assert.strictEqual(magicHits.length, 0,
            'expected no magic-value violations, even though 50.0 and 15.75 are unnamed literals');
    });
});
