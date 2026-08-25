import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, assertValidPositions } from './testUtils';

// decision: coherence is a whole-file metric (function count, large-function count,
// import count), unlike every other detector here - it can't be exercised with a
// "clean version + flagged version in one file" fixture, so each scenario gets its
// own file instead.
suite('Integration: file coherence (real code examples)', () => {
    for (const [label, language, ext] of [
        ['Python', PYTHON, 'py'],
        ['TypeScript', TYPESCRIPT, 'ts'],
        ['F#', FSHARP, 'fs'],
        ['Kotlin', KOTLIN, 'kt']
    ] as const) {
        test(`${label}: too many large functions is flagged`, async () => {
            const fixture = `${language.id}/coherence/manyLargeFunctions.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.find(v => v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('exceed'));
            assert.ok(hit, 'expected a large-function coherence violation for 6 functions over 20 lines each');
        });

        test(`${label}: import sprawl is flagged`, async () => {
            const fixture = `${language.id}/coherence/manyImports.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.find(v => v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('Import sprawl'));
            assert.ok(hit, 'expected an import-sprawl coherence violation for 11 imports');
        });

        test(`${label}: a small module stays quiet`, async () => {
            const fixture = `${language.id}/coherence/clean.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.filter(v => v.type === VIOLATION_TYPE.COHERENCE);
            assert.strictEqual(hit.length, 0, `expected no coherence violations, got: ${JSON.stringify(hit)}`);
        });
    }
});
