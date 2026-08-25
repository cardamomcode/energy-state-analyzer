import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: primitive obsession (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/primitiveObsession.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/primitiveObsession.ts'],
        ['F#', FSHARP, 'fsharp/primitiveObsession.fs'],
        ['Kotlin', KOTLIN, 'kotlin/primitiveObsession.kt']
    ] as const) {
        test(`${label}: distinct parameter types stay clean; same-type params and stringly-typed control flow are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanDistinctTypes');
            const swapRisk = findFunctionRange(sourceCode, 'flaggedSwapRisk');
            const stringly = findFunctionRange(sourceCode, 'flaggedStringlyTyped');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.PRIMITIVE_OBSESSION).length, 0,
                'a string and an int parameter should not be flagged as swappable');

            const swapHit = violationsIn(violations, swapRisk).filter(v => v.type === VIOLATION_TYPE.PRIMITIVE_OBSESSION);
            assert.ok(swapHit.some(v => v.message.includes('swap')), 'expected a swap-risk violation for two consecutive int params');

            const stringlyHit = violationsIn(violations, stringly).filter(v => v.type === VIOLATION_TYPE.PRIMITIVE_OBSESSION);
            assert.ok(stringlyHit.some(v => v.message.includes('Stringly-typed')),
                'expected a stringly-typed-control-flow violation for 3 distinct string comparisons');
        });
    }

    // Python-only: `x in (a, b, c)` membership checks have no direct equivalent in
    // F#'s or TypeScript's grammars (see README Known Issues), so this sub-check
    // only has a fixture/test for Python.
    test('Python: variable checked against a literal tuple in one `in` expression is flagged', async () => {
        const fixture = 'python/primitiveObsession.py';
        const { sourceCode, tree } = await parseFixture(PYTHON, fixture);
        const violations = analyzeSource(sourceCode, tree, PYTHON, fixture);
        assertValidPositions(violations, sourceCode);

        const membership = findFunctionRange(sourceCode, 'flaggedMembershipCheck');
        const membershipHit = violationsIn(violations, membership).filter(v => v.type === VIOLATION_TYPE.PRIMITIVE_OBSESSION);
        assert.ok(membershipHit.some(v => v.message.includes('Stringly-typed')),
            'expected a stringly-typed-control-flow violation for a 3-element `in (...)` membership check');
    });

    test('Python: keyword-only same-typed params are not flagged as swappable', async () => {
        const fixture = 'python/primitiveObsession.py';
        const { sourceCode, tree } = await parseFixture(PYTHON, fixture);
        const violations = analyzeSource(sourceCode, tree, PYTHON, fixture);
        assertValidPositions(violations, sourceCode);

        const suppressed = findFunctionRange(sourceCode, 'suppressedKeywordOnly');
        assert.strictEqual(
            violationsIn(violations, suppressed).filter(v => v.type === VIOLATION_TYPE.PRIMITIVE_OBSESSION).length, 0,
            'params after a bare `*` cannot be called positionally, so swap risk does not apply'
        );

        const suppressedAfterStar = findFunctionRange(sourceCode, 'suppressedAfterStarArgs');
        assert.strictEqual(
            violationsIn(violations, suppressedAfterStar).filter(v => v.type === VIOLATION_TYPE.PRIMITIVE_OBSESSION).length, 0,
            'params after `*args` are also keyword-only and cannot be called positionally'
        );

        const partial = findFunctionRange(sourceCode, 'flaggedPartiallyKeywordOnly');
        const partialHit = violationsIn(violations, partial).filter(v => v.type === VIOLATION_TYPE.PRIMITIVE_OBSESSION);
        assert.ok(partialHit.some(v => v.message.includes('swap')),
            'only one of the two same-typed params is keyword-only, so a positional call is still possible');
    });
});
