import * as fs from 'fs';
import * as path from 'path';

// .esaignore holds one path pattern per line, relative to the file's own directory (the
// project root, by convention — same as .gitignore). Blank lines and lines starting with
// '#' are skipped.
//
// decision: supports exactly two pattern shapes — a literal path/directory (matches
// itself, and everything under it if it's a directory; a bare name with no '/' also
// matches at any depth, like gitignore) and a single-segment basename glob containing '*'
// (e.g. `*.generated.ts`) — not a full gitignore engine (no negation, no '**', no brace
// expansion), matching scan.ts's existing minimal-glob philosophy (see its
// expandGlobLike) rather than pulling in a dependency.
export const ESAIGNORE_FILENAME = '.esaignore';

export function loadIgnorePatterns(rootDir: string): string[] {
    const ignorePath = path.join(rootDir, ESAIGNORE_FILENAME);
    if (!fs.existsSync(ignorePath)) {
        return [];
    }
    return fs
        .readFileSync(ignorePath, 'utf8')
        .split('\n')
        .map((line) => line.trim())
        .filter((line) => line.length > 0 && !line.startsWith('#'))
        .map((line) => line.replace(/\/+$/, ''));
}

function matchesLiteralPattern(relPath: string, pattern: string): boolean {
    if (pattern.includes('/')) {
        return relPath === pattern || relPath.startsWith(`${pattern}/`);
    }
    return relPath.split('/').includes(pattern);
}

function basenameGlobToRegExp(pattern: string): RegExp {
    const escaped = pattern
        .split('*')
        .map((part) => part.replace(/[.+?^${}()|[\]\\]/g, '\\$&'))
        .join('.*');
    return new RegExp(`^${escaped}$`);
}

// Whether `absolutePath` (a file or directory) matches any pattern loaded via
// loadIgnorePatterns, rooted at `rootDir`.
export function isIgnored(absolutePath: string, rootDir: string, patterns: string[]): boolean {
    if (patterns.length === 0) {
        return false;
    }
    const relPath = path.relative(rootDir, absolutePath).split(path.sep).join('/');
    const basename = path.basename(absolutePath);

    return patterns.some((pattern) =>
        pattern.includes('*') ? basenameGlobToRegExp(pattern).test(basename) : matchesLiteralPattern(relPath, pattern)
    );
}
