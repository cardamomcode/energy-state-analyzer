import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: inversion opportunities (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/inversion.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/inversion.ts'],
        ['Kotlin', KOTLIN, 'kotlin/inversion.kt']
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

    // decision: TS-only — Python's `for x in y` is a single for_statement node type covering
    // both classic and "for-of"-style iteration, so the bug this guards against (a for-of loop
    // going unrecognized because nodeTypes.forStatement only covers TS's separate for_statement,
    // not its for_in_statement) can only reproduce here
    test('TypeScript: a for-of loop sibling to a 2-deep nested if is not mistaken for a validation chain', async () => {
        const { sourceCode, tree } = await parseFixture(TYPESCRIPT, 'typescript/inversion.ts');
        const violations = analyzeSource(sourceCode, tree, TYPESCRIPT, 'inversion.ts');
        assertValidPositions(violations, sourceCode);

        const clean = findFunctionRange(sourceCode, 'cleanForOfSibling');
        assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.INVERSION).length, 0,
            'a for-of loop sibling should disqualify the nested if from looking like a validation chain');
    });

    // decision: Kotlin-only — its else has no else_clause wrapper node (unlike TS/Python), so
    // "does this if have an else" can't be answered by checking one child's type; this guards
    // the fallback in inversion.ts's hasElse check (a second block child, or a nested if child)
    // that makes that possible without one. Without it, the outer if here would be wrongly
    // treated as an else-less guard step and the chain below it would be flagged.
    test('Kotlin: an if/else is not mistaken for the start of a guard-clause validation chain', async () => {
        const { sourceCode, tree } = await parseFixture(KOTLIN, 'kotlin/inversion.kt');
        const violations = analyzeSource(sourceCode, tree, KOTLIN, 'inversion.kt');
        assertValidPositions(violations, sourceCode);

        const clean = findFunctionRange(sourceCode, 'cleanIfElseNotValidationChain');
        assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.INVERSION).length, 0,
            'the outer if has an else, so it must not be treated as a validation-chain guard step');
    });

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
