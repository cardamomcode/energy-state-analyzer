import * as assert from 'assert';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';

import { resolveSupportedFiles } from '../../core/scan';
import { isIgnored, loadIgnorePatterns } from '../../core/esaignore';

suite('Integration: .esaignore', () => {
    let root: string;

    setup(() => {
        root = fs.mkdtempSync(path.join(os.tmpdir(), 'energy-esaignore-'));
        fs.mkdirSync(path.join(root, 'src', 'fixtures'), { recursive: true });
        fs.mkdirSync(path.join(root, 'src', 'real'), { recursive: true });

        fs.writeFileSync(path.join(root, 'src', 'fixtures', 'bad.py'), 'x = 1\n');
        fs.writeFileSync(path.join(root, 'src', 'real', 'good.py'), 'y = 2\n');
        fs.writeFileSync(path.join(root, 'src', 'real', 'good.generated.py'), 'z = 3\n');
    });

    teardown(() => {
        fs.rmSync(root, { recursive: true, force: true });
    });

    test('loadIgnorePatterns returns [] when no .esaignore file exists', () => {
        assert.deepStrictEqual(loadIgnorePatterns(root), []);
    });

    test('loadIgnorePatterns skips blank lines and comments, strips trailing slashes', () => {
        fs.writeFileSync(path.join(root, '.esaignore'), '\n# comment\nsrc/fixtures/\n*.generated.py\n');
        assert.deepStrictEqual(loadIgnorePatterns(root), ['src/fixtures', '*.generated.py']);
    });

    test('isIgnored matches a literal directory pattern as a prefix', () => {
        const patterns = ['src/fixtures'];
        assert.ok(isIgnored(path.join(root, 'src', 'fixtures', 'bad.py'), root, patterns));
        assert.ok(!isIgnored(path.join(root, 'src', 'real', 'good.py'), root, patterns));
    });

    test('isIgnored matches a bare name at any depth', () => {
        const patterns = ['fixtures'];
        assert.ok(isIgnored(path.join(root, 'src', 'fixtures', 'bad.py'), root, patterns));
    });

    test('isIgnored matches a basename glob', () => {
        const patterns = ['*.generated.py'];
        assert.ok(isIgnored(path.join(root, 'src', 'real', 'good.generated.py'), root, patterns));
        assert.ok(!isIgnored(path.join(root, 'src', 'real', 'good.py'), root, patterns));
    });

    test('resolveSupportedFiles excludes paths matched by a .esaignore at rootDir', () => {
        fs.writeFileSync(path.join(root, '.esaignore'), 'src/fixtures\n');

        const files = resolveSupportedFiles([root], root);
        const relative = files.map((f) => path.relative(root, f)).sort();

        assert.deepStrictEqual(
            relative,
            [path.join('src', 'real', 'good.generated.py'), path.join('src', 'real', 'good.py')].sort()
        );
    });
});
