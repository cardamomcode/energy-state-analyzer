import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE, SEVERITY } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: logical operator as control flow (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/logicalControlFlow.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/logicalControlFlow.ts']
    ] as const) {
        test(`${label}: an explicit if stays clean, '&&'/'||' used as a statement is flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanExplicitIf');
            const andAsIf = findFunctionRange(sourceCode, 'flaggedAndAsIf');
            const orAsUnless = findFunctionRange(sourceCode, 'flaggedOrAsUnless');

            assert.strictEqual(
                violationsIn(violations, clean).filter((v) => v.type === VIOLATION_TYPE.LOGICAL_CONTROL_FLOW).length,
                0,
                'an explicit if-statement should not be flagged'
            );

            const andHit = violationsIn(violations, andAsIf).filter(
                (v) => v.type === VIOLATION_TYPE.LOGICAL_CONTROL_FLOW
            );
            assert.ok(andHit.length > 0, "expected a logical-control-flow violation for '&&' used as a statement");
            assert.strictEqual(andHit[0].severity, SEVERITY.LOW);

            const orHit = violationsIn(violations, orAsUnless).filter(
                (v) => v.type === VIOLATION_TYPE.LOGICAL_CONTROL_FLOW
            );
            assert.ok(orHit.length > 0, "expected a logical-control-flow violation for '||' used as a statement");
            assert.strictEqual(orHit[0].severity, SEVERITY.LOW);
        });
    }

    test("F#: '&&' used as a statement is not flagged (no expressionStatement node in this grammar)", async () => {
        const { sourceCode, tree } = await parseFixture(FSHARP, 'fsharp/logicalControlFlow.fs');
        const violations = analyzeSource(sourceCode, tree, FSHARP, 'fsharp/logicalControlFlow.fs');
        assertValidPositions(violations, sourceCode);

        assert.strictEqual(
            violations.filter((v) => v.type === VIOLATION_TYPE.LOGICAL_CONTROL_FLOW).length,
            0,
            "F#'s LanguageAdapter has nodeTypes.expressionStatement: null, so this detector never fires for it"
        );
    });

    test("Kotlin: '&&' used as a statement is not flagged (no expressionStatement node in this grammar)", async () => {
        const { sourceCode, tree } = await parseFixture(KOTLIN, 'kotlin/logicalControlFlow.kt');
        const violations = analyzeSource(sourceCode, tree, KOTLIN, 'kotlin/logicalControlFlow.kt');
        assertValidPositions(violations, sourceCode);

        assert.strictEqual(
            violations.filter((v) => v.type === VIOLATION_TYPE.LOGICAL_CONTROL_FLOW).length,
            0,
            "Kotlin's bare `a && b()` statement is a direct binary_expression child of block, with no expression_statement wrapper to key off, same gap as F#"
        );
    });
});
