import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: inversion opportunities (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/inversion.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/inversion.ts']
    ] as const) {
        test(`${label}: early-return guard clauses stay clean; a dominant if-block and a nested validation chain are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanEarlyReturn');
            const dominant = findFunctionRange(sourceCode, 'flaggedDominantIf');
            const chain = findFunctionRange(sourceCode, 'flaggedValidationChain');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.INVERSION).length, 0,
                'two independent guard clauses should not be flagged');

            assert.ok(violationsIn(violations, dominant).some(v => v.type === VIOLATION_TYPE.INVERSION),
                'expected an inversion violation for the if-block that dominates the function body');

            assert.ok(violationsIn(violations, chain).some(v => v.type === VIOLATION_TYPE.INVERSION),
                'expected an inversion violation for the 3-deep nested validation chain');
        });
    }

    // decision: documents a known adapter limitation (see fsharp.ts's nodeTypes.block:
    // null) rather than asserting the "expected" behavior - analyzeInversionOpportunities
    // looks up a function's body via nodeTypes.block, which F# has no equivalent for, so
    // it returns before running any of its three patterns. This locks in that current
    // behavior as a regression test; if F# ever gets inversion support, this test should
    // start failing and can be flipped to expect a violation.
    test('F#: inversion opportunities are never flagged (documented limitation)', async () => {
        const { sourceCode, tree } = await parseFixture(FSHARP, 'fsharp/inversion.fs');
        const violations = analyzeSource(sourceCode, tree, FSHARP, 'inversion.fs');
        assertValidPositions(violations, sourceCode);

        const hits = violations.filter(v => v.type === VIOLATION_TYPE.INVERSION);
        assert.strictEqual(hits.length, 0,
            'expected no inversion violations, even though this is a 3-deep nested validation chain');
    });
});
