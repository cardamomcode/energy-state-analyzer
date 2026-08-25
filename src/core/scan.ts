import * as fs from 'fs';
import * as path from 'path';

import { resolveLanguageForFile } from '../languages';

// invariant: this module only touches the filesystem (fs/path) — no tree-sitter, no vscode,
// no git — so it stays independently testable against src/test/fixtures

const IGNORED_DIR_NAMES = new Set(['node_modules', '.git', 'dist', 'out', 'build', '.next', 'coverage', '.vscode-test']);

function walkDirectory(dir: string, results: string[]): void {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            if (!IGNORED_DIR_NAMES.has(entry.name)) {
                walkDirectory(fullPath, results);
            }
        } else if (entry.isFile() && resolveLanguageForFile(entry.name)) {
            results.push(fullPath);
        }
    }
}

// decision: supports exactly one glob shape — a trailing `**/*.ext`-style suffix on an
// otherwise literal directory prefix — rather than pulling in a general glob-matching
// dependency. This covers the common "src/**/*.py" case; anything with brace expansion,
// negation, or mid-path wildcards is not a glob engine this function implements.
function expandGlobLike(pattern: string): string[] {
    const starIndex = pattern.indexOf('*');
    const prefixEnd = pattern.lastIndexOf(path.sep, starIndex);
    const prefixDir = prefixEnd === -1 ? '.' : pattern.slice(0, prefixEnd);
    const suffix = pattern.slice(pattern.lastIndexOf('.'));
    const extension = suffix.startsWith('.') && !suffix.includes('*') ? suffix : undefined;

    if (!fs.existsSync(prefixDir) || !fs.statSync(prefixDir).isDirectory()) {
        return [];
    }

    const results: string[] = [];
    walkDirectory(prefixDir, results);
    return extension ? results.filter(file => file.toLowerCase().endsWith(extension.toLowerCase())) : results;
}

// Expands file/directory/glob-like CLI arguments into a deduplicated, sorted list of
// absolute paths to files with a supported extension (see resolveLanguageForFile).
export function resolveSupportedFiles(inputs: string[]): string[] {
    const results: string[] = [];

    for (const input of inputs) {
        if (input.includes('*')) {
            results.push(...expandGlobLike(input));
            continue;
        }

        if (!fs.existsSync(input)) {
            continue;
        }

        const stat = fs.statSync(input);
        if (stat.isDirectory()) {
            walkDirectory(input, results);
        } else if (stat.isFile() && resolveLanguageForFile(input)) {
            results.push(input);
        }
    }

    const absolute = results.map(file => path.resolve(file));
    return Array.from(new Set(absolute)).sort();
}
