import * as fs from 'fs';
import * as path from 'path';

import { resolveLanguageForFile } from '../languages';
import { isIgnored, loadIgnorePatterns } from './esaignore';

// invariant: this module only touches the filesystem (fs/path) — no tree-sitter, no vscode,
// no git — so it stays independently testable against src/test/fixtures

const IGNORED_DIR_NAMES = new Set([
    'node_modules',
    '.git',
    'dist',
    'out',
    'build',
    '.next',
    'coverage',
    '.vscode-test'
]);

// decision: bundles rootDir + ignorePatterns rather than passing them as two adjacent
// string/string[] parameters — they always travel together, and rootDir sitting next to
// another string parameter (dir, pattern) at each call site is exactly the swap-risk shape
// the primitive-obsession detector flags (two consecutive same-typed params a caller could
// transpose without the type checker complaining)
interface IgnoreContext {
    rootDir: string;
    ignorePatterns: string[];
}

function isPathIgnored(targetPath: string, ignore: IgnoreContext): boolean {
    return isIgnored(targetPath, ignore.rootDir, ignore.ignorePatterns);
}

function walkDirectory(dir: string, ignore: IgnoreContext, results: string[]): void {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const fullPath = path.join(dir, entry.name);
        if (IGNORED_DIR_NAMES.has(entry.name) || isPathIgnored(fullPath, ignore)) {
            continue;
        }
        if (entry.isDirectory()) {
            walkDirectory(fullPath, ignore, results);
        } else if (entry.isFile() && resolveLanguageForFile(entry.name)) {
            results.push(fullPath);
        }
    }
}

// decision: supports exactly one glob shape — a trailing `**/*.ext`-style suffix on an
// otherwise literal directory prefix — rather than pulling in a general glob-matching
// dependency. This covers the common "src/**/*.py" case; anything with brace expansion,
// negation, or mid-path wildcards is not a glob engine this function implements.
function expandGlobLike(pattern: string, ignore: IgnoreContext): string[] {
    const starIndex = pattern.indexOf('*');
    const prefixEnd = pattern.lastIndexOf(path.sep, starIndex);
    const prefixDir = prefixEnd === -1 ? '.' : pattern.slice(0, prefixEnd);
    const suffix = pattern.slice(pattern.lastIndexOf('.'));
    const extension = suffix.startsWith('.') && !suffix.includes('*') ? suffix : undefined;

    if (!fs.existsSync(prefixDir) || !fs.statSync(prefixDir).isDirectory()) {
        return [];
    }

    const results: string[] = [];
    walkDirectory(prefixDir, ignore, results);
    return extension ? results.filter((file) => file.toLowerCase().endsWith(extension.toLowerCase())) : results;
}

// Expands file/directory/glob-like CLI arguments into a deduplicated, sorted list of
// absolute paths to files with a supported extension (see resolveLanguageForFile),
// excluding anything matched by a `.esaignore` file found in `rootDir` (defaults to the
// current working directory, mirroring where a CLI invocation expects to find it).
export function resolveSupportedFiles(inputs: string[], rootDir: string = process.cwd()): string[] {
    const ignore: IgnoreContext = { rootDir, ignorePatterns: loadIgnorePatterns(rootDir) };
    const results: string[] = [];

    for (const input of inputs) {
        if (input.includes('*')) {
            results.push(...expandGlobLike(input, ignore));
            continue;
        }

        if (!fs.existsSync(input)) {
            continue;
        }

        const stat = fs.statSync(input);
        if (stat.isDirectory()) {
            if (!isPathIgnored(input, ignore)) {
                walkDirectory(input, ignore, results);
            }
        } else if (stat.isFile() && resolveLanguageForFile(input) && !isPathIgnored(input, ignore)) {
            results.push(input);
        }
    }

    const absolute = results.map((file) => path.resolve(file));
    return Array.from(new Set(absolute)).sort();
}
