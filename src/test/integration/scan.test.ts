import * as assert from 'assert';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';

import { resolveSupportedFiles } from '../../core/scan';

// decision: builds a synthetic directory tree under os.tmpdir() rather than reusing
// src/test/fixtures — the ignore-list/glob behavior under test needs a node_modules-shaped
// tree, and fixtures/ has no reason to carry one for real detector tests
suite('Integration: scan (resolveSupportedFiles)', () => {
    let root: string;

    setup(() => {
        root = fs.mkdtempSync(path.join(os.tmpdir(), 'energy-scan-'));
        fs.mkdirSync(path.join(root, 'src', 'nested'), { recursive: true });
        fs.mkdirSync(path.join(root, 'node_modules', 'dep'), { recursive: true });
        fs.mkdirSync(path.join(root, 'dist'), { recursive: true });

        fs.writeFileSync(path.join(root, 'top.py'), 'x = 1\n');
        fs.writeFileSync(path.join(root, 'src', 'nested', 'inner.py'), 'y = 2\n');
        fs.writeFileSync(path.join(root, 'src', 'nested', 'inner.ts'), 'const z = 3;\n');
        fs.writeFileSync(path.join(root, 'src', 'README.md'), '# not a supported extension\n');
        fs.writeFileSync(path.join(root, 'node_modules', 'dep', 'vendored.py'), 'should_be_ignored = 1\n');
        fs.writeFileSync(path.join(root, 'dist', 'built.py'), 'should_be_ignored = 2\n');
    });

    teardown(() => {
        fs.rmSync(root, { recursive: true, force: true });
    });

    test('recurses into directories and filters to supported extensions', () => {
        const files = resolveSupportedFiles([root]);
        const relative = files.map((f) => path.relative(root, f)).sort();

        assert.deepStrictEqual(
            relative,
            [path.join('src', 'nested', 'inner.py'), path.join('src', 'nested', 'inner.ts'), 'top.py'].sort()
        );
    });

    test('skips node_modules, dist, and other ignored directory names', () => {
        const files = resolveSupportedFiles([root]);
        assert.ok(!files.some((f) => f.includes('node_modules')), 'node_modules should be skipped');
        assert.ok(!files.some((f) => f.includes(`${path.sep}dist${path.sep}`)), 'dist should be skipped');
    });

    test('accepts an explicit single file regardless of extension support', () => {
        const files = resolveSupportedFiles([path.join(root, 'top.py')]);
        assert.deepStrictEqual(files, [path.resolve(path.join(root, 'top.py'))]);
    });

    test('silently drops an explicit file with an unsupported extension', () => {
        const files = resolveSupportedFiles([path.join(root, 'src', 'README.md')]);
        assert.deepStrictEqual(files, []);
    });

    test('supports a single trailing **/*.ext glob shape', () => {
        const files = resolveSupportedFiles([path.join(root, 'src', '**', '*.py')]);
        const relative = files.map((f) => path.relative(root, f)).sort();
        assert.deepStrictEqual(relative, [path.join('src', 'nested', 'inner.py')]);
    });

    test('deduplicates and sorts results across overlapping inputs', () => {
        const files = resolveSupportedFiles([root, path.join(root, 'top.py')]);
        const topOccurrences = files.filter((f) => f === path.resolve(path.join(root, 'top.py')));
        assert.strictEqual(topOccurrences.length, 1, 'top.py should not be duplicated');
        assert.deepStrictEqual(files, [...files].sort(), 'results should be sorted');
    });

    test('ignores nonexistent paths without throwing', () => {
        assert.doesNotThrow(() => resolveSupportedFiles([path.join(root, 'does-not-exist.py')]));
        assert.deepStrictEqual(resolveSupportedFiles([path.join(root, 'does-not-exist.py')]), []);
    });
});
