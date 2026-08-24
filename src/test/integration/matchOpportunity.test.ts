import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: match opportunities (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/matchOpportunity.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/matchOpportunity.ts'],
        ['F#', FSHARP, 'fsharp/matchOpportunity.fs']
    ] as const) {
        test(`${label}: an if/elif chain on unrelated variables stays clean; a 3-way chain on one variable is flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanMixedConditions');
            const chain = findFunctionRange(sourceCode, 'flaggedThreeWayChain');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.MATCH_OPPORTUNITY).length, 0,
                'an if/elif chain branching on different variables should not be flagged');

            const chainHit = violationsIn(violations, chain).filter(v => v.type === VIOLATION_TYPE.MATCH_OPPORTUNITY);
            assert.ok(chainHit.some(v => v.message.includes('status')),
                'expected a match-opportunity violation for the 3-way chain on status');
        });
    }
});
