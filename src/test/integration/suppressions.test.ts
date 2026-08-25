import * as assert from 'assert';

import { applySuppressions, parseSuppressions } from '../../core/suppressions';
import { EnergyViolation, VIOLATION_TYPE } from '../../types';

function violation(line: number, type: EnergyViolation['type'] = VIOLATION_TYPE.NESTING): EnergyViolation {
    return { line, column: 0, type, severity: 'medium', message: 'test' };
}

suite('Integration: esa-ignore suppressions', () => {
    test('bare esa-ignore on the same line suppresses every type on that line', () => {
        const source = 'if deeply_nested():  # esa-ignore\n';
        const { violations, suppressionNotes } = applySuppressions([violation(0, VIOLATION_TYPE.NESTING)], source);

        assert.strictEqual(violations.length, 0);
        assert.strictEqual(suppressionNotes.length, 0);
    });

    test('typed esa-ignore only suppresses the listed type(s)', () => {
        const source = 'if deeply_nested():  // esa-ignore: nesting\n';
        const { violations } = applySuppressions(
            [violation(0, VIOLATION_TYPE.NESTING), violation(0, VIOLATION_TYPE.COMPLEXITY)],
            source
        );

        assert.strictEqual(violations.length, 1);
        assert.strictEqual(violations[0].type, VIOLATION_TYPE.COMPLEXITY);
    });

    test('a standalone directive also covers the line below it', () => {
        const source = [
            '// esa-ignore: complexity',
            'function reallyBigOne(a, b, c, d, e, f) {',
            '}'
        ].join('\n');
        const { violations } = applySuppressions([violation(1, VIOLATION_TYPE.COMPLEXITY)], source);

        assert.strictEqual(violations.length, 0);
    });

    test('a trailing directive does NOT cover the line below it', () => {
        const source = [
            'const x = 1;  // esa-ignore: complexity',
            'function reallyBigOne(a, b, c, d, e, f) {',
            '}'
        ].join('\n');
        const { violations } = applySuppressions([violation(1, VIOLATION_TYPE.COMPLEXITY)], source);

        assert.strictEqual(violations.length, 1, 'the directive shares a line with code, so it should not reach downward');
    });

    test('esa-ignore-file suppresses a type anywhere in the file, e.g. file-scoped coherence violations', () => {
        const source = '# esa-ignore-file: coherence\n';
        const { violations } = applySuppressions(
            [violation(0, VIOLATION_TYPE.COHERENCE), violation(40, VIOLATION_TYPE.COHERENCE)],
            source
        );

        assert.strictEqual(violations.length, 0);
    });

    test('an unused directive is reported as a low-severity suppression note instead of vanishing silently', () => {
        const source = 'return 1;  # esa-ignore: magic\n';
        const { violations, suppressionNotes } = applySuppressions([], source);

        assert.strictEqual(violations.length, 0);
        assert.strictEqual(suppressionNotes.length, 1);
        assert.strictEqual(suppressionNotes[0].type, VIOLATION_TYPE.SUPPRESSION);
        assert.strictEqual(suppressionNotes[0].severity, 'low');
        assert.match(suppressionNotes[0].message, /unused/i);
    });

    test('an unknown type name is reported, independent of whether the directive also matched something', () => {
        const source = 'return 1;  # esa-ignore: nseting\n';
        const { suppressionNotes } = applySuppressions([violation(0, VIOLATION_TYPE.NESTING)], source);

        assert.strictEqual(suppressionNotes.length, 2, 'expect both an unknown-type note and an unused-directive note');
        assert.ok(suppressionNotes.some(note => /unknown/i.test(note.message)));
    });

    test('parseSuppressions recognizes both // and # comment styles', () => {
        const suppressions = parseSuppressions('a  // esa-ignore\nb  # esa-ignore-file: magic\n');

        assert.strictEqual(suppressions.length, 2);
        assert.strictEqual(suppressions[0].scope, 'line');
        assert.strictEqual(suppressions[1].scope, 'file');
        assert.deepStrictEqual(suppressions[1].types, [VIOLATION_TYPE.MAGIC]);
    });
});
