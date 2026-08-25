import * as assert from 'assert';

import { analyzeSource } from '../../core/analyze';
import { PYTHON } from '../../languages/python';
import { TYPESCRIPT } from '../../languages/typescript';
import { FSHARP } from '../../languages/fsharp';
import { KOTLIN } from '../../languages/kotlin';
import { VIOLATION_TYPE } from '../../types';
import { parseFixture, findFunctionRange, violationsIn, assertValidPositions } from './testUtils';

suite('Integration: magic numbers (real code examples)', () => {
    for (const [label, language, fixture] of [
        ['Python', PYTHON, 'python/magicNumber.py'],
        ['TypeScript', TYPESCRIPT, 'typescript/magicNumber.ts'],
        ['F#', FSHARP, 'fsharp/magicNumber.fs'],
        ['Kotlin', KOTLIN, 'kotlin/magicNumber.kt']
    ] as const) {
        test(`${label}: allowlisted values stay clean, significant literals are flagged`, async () => {
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            assertValidPositions(violations, sourceCode);

            const clean = findFunctionRange(sourceCode, 'cleanCommonValues');
            const numbers = findFunctionRange(sourceCode, 'flaggedMagicNumbers');
            const negative = findFunctionRange(sourceCode, 'cleanNegativeValue');

            assert.strictEqual(violationsIn(violations, clean).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
                '0 and 1 are on the default allowlist and a named constant binding should not be flagged');

            assert.strictEqual(violationsIn(violations, negative).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
                '-1 is on the default allowlist and should not be flagged regardless of how the grammar represents the sign');

            // decision: pins the exact count, not just ">0" — regression guard for a bug where
            // F#'s recursive float grammar (each digit-group fragment is itself typed `float`)
            // and TS's `number`-typed `predefined_type` keyword both caused the same literal to
            // be visited, and flagged, more than once
            const numberHits = violationsIn(violations, numbers).filter(v => v.type === VIOLATION_TYPE.MAGIC);
            assert.strictEqual(numberHits.length, 3, 'expected exactly one violation each for 1.08, 50, and 15.75');
        });
    }

    test('Python: a module-level assignment (wrapped in expression_statement) is a constant binding', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicNumber.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicNumber.py');
        assert.strictEqual(violations.filter(v => v.line === 0 && v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'MAX_RETRIES = 5 at module scope should not be flagged even though Python wraps the assignment in an expression_statement before the module root');
    });

    test('F#: a module-level `let` binding is a constant binding', async () => {
        const { sourceCode, tree } = await parseFixture(FSHARP, 'fsharp/magicNumber.fs');
        const violations = analyzeSource(sourceCode, tree, FSHARP, 'magicNumber.fs');
        const maxRetries = findFunctionRange(sourceCode, 'maxRetries');
        assert.strictEqual(violationsIn(violations, maxRetries).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'let maxRetries = 5 at module scope should not be flagged: any literal directly bound by a let is treated as named');
    });

    test('Kotlin: a module-level `val` binding is a constant binding', async () => {
        const { sourceCode, tree } = await parseFixture(KOTLIN, 'kotlin/magicNumber.kt');
        const violations = analyzeSource(sourceCode, tree, KOTLIN, 'magicNumber.kt');
        assert.strictEqual(violations.filter(v => v.line === 0 && v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'const val MAX_RETRIES = 5 at module scope should not be flagged: assignment maps to property_declaration, not the bare-reassignment `assignment` node');
    });

    test('Kotlin: a `const val` inside a companion object is a constant binding regardless of nesting', async () => {
        const { sourceCode, tree } = await parseFixture(KOTLIN, 'kotlin/magicNumber.kt');
        const violations = analyzeSource(sourceCode, tree, KOTLIN, 'magicNumber.kt');
        const limits = findFunctionRange(sourceCode, 'Limits');
        assert.strictEqual(violationsIn(violations, limits).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'const val inside a companion object is a real compile-time constant, not just a module-scope heuristic match, so it should stay exempt at any nesting depth');
    });

    for (const [label, language] of [
        ['Python', PYTHON],
        ['TypeScript', TYPESCRIPT],
        ['Kotlin', KOTLIN]
    ] as const) {
        test(`${label}: array index and default parameter value are exempt`, async () => {
            const fixture = `${language.id}/magicNumber.${language === PYTHON ? 'py' : language === TYPESCRIPT ? 'ts' : 'kt'}`;
            const { sourceCode, tree } = await parseFixture(language, fixture);
            const violations = analyzeSource(sourceCode, tree, language, fixture);
            const exempt = findFunctionRange(sourceCode, 'exemptIndexAndDefault');
            assert.strictEqual(violationsIn(violations, exempt).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
                'arr[0] and a default parameter value of 42 should not be flagged');
        });
    }
    // decision: F# has no dedicated subscript node and its parameter grammar carries no
    // default-value concept the adapter models — this is a documented gap (see README), not
    // tested here since there's no exempt case for F# to assert against.

    test('magicNumber.enabled: false suppresses all magic-number violations', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicNumber.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicNumber.py', {
            magicNumber: { enabled: false, allowlist: [] }
        });
        assert.strictEqual(violations.filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'disabling the detector should leave no magic-number violations');
    });

    test('magicNumber.allowlist: a custom allowlist exempts additional values', async () => {
        const { sourceCode, tree } = await parseFixture(PYTHON, 'python/magicNumber.py');
        const violations = analyzeSource(sourceCode, tree, PYTHON, 'magicNumber.py', {
            magicNumber: { enabled: true, allowlist: [0, 1, -1, 2, 50, 15.75, 1.08] }
        });
        const numbers = findFunctionRange(sourceCode, 'flaggedMagicNumbers');
        assert.strictEqual(violationsIn(violations, numbers).filter(v => v.type === VIOLATION_TYPE.MAGIC).length, 0,
            'literals added to the allowlist should no longer be flagged');
    });
});
