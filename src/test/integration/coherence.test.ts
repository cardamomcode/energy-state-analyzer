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

            const hit = violations.find((v) => v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('exceed'));
            assert.ok(hit, 'expected a large-function coherence violation for 6 functions over 20 lines each');
        });

        test(`${label}: import sprawl is flagged`, async () => {
            const fixture = `${language.id}/coherence/manyImports.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.find(
                (v) => v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('Import sprawl')
            );
            assert.ok(hit, 'expected an import-sprawl coherence violation for 11 imports');
        });

        test(`${label}: many imports from one source stays quiet`, async () => {
            const fixture = `${language.id}/coherence/narrowImports.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.find(
                (v) => v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('Import sprawl')
            );
            assert.ok(
                !hit,
                `expected no import-sprawl violation for 11 import lines drawing from one real dependency, got: ${JSON.stringify(hit)}`
            );
        });

        test(`${label}: a small module stays quiet`, async () => {
            const fixture = `${language.id}/coherence/clean.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.filter((v) => v.type === VIOLATION_TYPE.COHERENCE);
            assert.strictEqual(hit.length, 0, `expected no coherence violations, got: ${JSON.stringify(hit)}`);
        });

        // decision: regression guard for a real false positive - an F#-style module exposing
        // one verb per operation (map/filter/fold/...) over a shared domain type, no naming
        // cohesion at all, well past the generic 12-function threshold. Confirmed to
        // misfire under the naming-only heuristic before the type-cohesion signal existed
        // (see coherence.ts's decision comments).
        test(`${label}: a type-cohesive module with no naming cohesion stays quiet`, async () => {
            const fixture = `${language.id}/coherence/typeCohesive.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.filter((v) => v.type === VIOLATION_TYPE.COHERENCE);
            assert.strictEqual(
                hit.length,
                0,
                `expected no coherence violations for a type-cohesive module, got: ${JSON.stringify(hit)}`
            );
        });

        // decision: confirms the type signal produces the stronger, more specific message
        // once a file is already past the existing function-count threshold (12, same as the
        // naming-cohesion check) and genuinely spans unrelated types - not a case of a shared
        // type family the naming heuristic alone would have missed.
        test(`${label}: a module with distinct names AND unrelated types gets the stronger entropy-dump message`, async () => {
            const fixture = `${language.id}/coherence/entropyDump.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.find(
                (v) => v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('unrelated types')
            );
            assert.ok(hit, 'expected an entropy-dump coherence violation for 13 functions spanning unrelated types');
        });
    }

    // decision: only languages with a real class construct - F# has no classDefinitionNodeTypes
    // (see fsharp.ts), so it's excluded from this block rather than given empty fixtures.
    for (const [label, language, ext] of [
        ['Python', PYTHON, 'py'],
        ['TypeScript', TYPESCRIPT, 'ts'],
        ['Kotlin', KOTLIN, 'kt']
    ] as const) {
        // decision: regression guard for a real false positive - methods were being counted
        // like free-standing functions and their few incidental parameter/return types (each
        // other's own class) misread as "unrelated types", flagging a 2-class file with 15
        // methods total as function-count sprawl. Classes must be grouped by enclosing type
        // before the type-diversity signal is evaluated.
        test(`${label}: two classes that construct/return each other stay quiet`, async () => {
            const fixture = `${language.id}/coherence/relatedClasses.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.filter((v) => v.type === VIOLATION_TYPE.COHERENCE);
            assert.strictEqual(
                hit.length,
                0,
                `expected no coherence violations for two type-linked classes, got: ${JSON.stringify(hit)}`
            );
        });

        test(`${label}: two classes with no shared inheritance, type reference, or naming pattern are flagged`, async () => {
            const fixture = `${language.id}/coherence/unrelatedClasses.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.find(
                (v) => v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('unrelated groups')
            );
            assert.ok(hit, 'expected an unrelated-classes coherence violation for Logger/HttpClient');
        });

        // decision: regression guard for an exceptions.py-style file - classes with no
        // cross-reference and no shared naming affix, but sharing a common base class (even
        // one not defined in this file, e.g. Exception/Error) must stay quiet.
        test(`${label}: classes sharing a common base with no naming pattern stay quiet`, async () => {
            const fixture = `${language.id}/coherence/exceptionFamily.${ext}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const hit = violations.filter((v) => v.type === VIOLATION_TYPE.COHERENCE);
            assert.strictEqual(
                hit.length,
                0,
                `expected no coherence violations for a shared-base exception family, got: ${JSON.stringify(hit)}`
            );
        });
    }
});
