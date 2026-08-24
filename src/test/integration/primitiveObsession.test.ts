import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: primitive obsession (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/primitiveObsession.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/primitiveObsession.ts'],
        ['F#', FSHARP, 'fsharp/primitiveObsession.fs']
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
});
