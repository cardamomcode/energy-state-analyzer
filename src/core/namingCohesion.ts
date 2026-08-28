// decision: splits on underscores AND camelCase/acronym boundaries (extractFoo -> [extract,
// foo], parse_json -> [parse, json], URLParser -> [url, parser]) rather than a plain leading
// `[a-z]+` run, so a word boundary is recognized regardless of the file's naming convention.
const WORD_BOUNDARY_PATTERN = /[A-Z]+(?=[A-Z][a-z])|[A-Z]?[a-z]+|[A-Z]+|[0-9]+/g;

function splitIntoWords(text: string): string[] {
    const matches = text.match(WORD_BOUNDARY_PATTERN);
    return matches ? matches.map((word) => word.toLowerCase()) : [];
}

// decision: splits the basename into words (on `_` and camelCase boundaries) and requires an
// exact word match, rather than a substring `includes` check — `includes('common')` would
// also match an unrelated file like `commonwealth.ts`, and `includes('util')` would match
// `futuleName.ts`. Word-splitting avoids both.
const UTILS_FILE_WORDS = new Set(['util', 'utils', 'helper', 'helpers', 'common']);

export function isUtilsFileName(fileName: string): boolean {
    const baseName = fileName.split('/').pop() || '';
    const withoutExtension = baseName.replace(/\.[^.]+$/, '');
    return splitIntoWords(withoutExtension).some((word) => UTILS_FILE_WORDS.has(word));
}

// decision: a raw function name is a weak signal on its own, but a *dominant leading or
// trailing word* shared across most of a file's functions (extractFoo/extractBar, or
// fooParser/barParser) is a cheap, AST-only proxy for "this file is one coherent domain
// factored into many small steps" — exactly the case a raw function-count sprawl check would
// otherwise misflag, since it can't tell that apart from an actual grab-bag of unrelated
// helpers by count alone. Checking both ends (not leading only) also catches naming
// conventions that put the domain word last (parseDate/formatDate), not just first.
function functionNameWords(node: any): string[] {
    const nameNode = node.children?.find((c: any) => c.type === 'identifier');
    if (!nameNode?.text) {
        return [];
    }
    return splitIntoWords(nameNode.text);
}

function dominantShare(words: string[]): number {
    if (words.length === 0) {
        return 0;
    }

    const wordCounts = new Map<string, number>();
    for (const word of words) {
        wordCounts.set(word, (wordCounts.get(word) ?? 0) + 1);
    }

    return Math.max(...wordCounts.values()) / words.length;
}

// decision: shared by looksLikeSingleDomain (functions) and looksLikeSingleDomainByNames
// (classes, see coherence.ts's checkClassRelatedness) — both want "does a dominant leading or
// trailing word-boundary chunk recur across most of these names", just starting from a
// different source (a function's identifier child vs. a class's already-extracted name string).
export function looksLikeSingleDomainByNames(names: string[], minShare: number): boolean {
    const leadingWords: string[] = [];
    const trailingWords: string[] = [];
    for (const name of names) {
        const words = splitIntoWords(name);
        if (words.length === 0) {
            continue;
        }
        leadingWords.push(words[0]);
        trailingWords.push(words[words.length - 1]);
    }

    if (leadingWords.length === 0) {
        return false;
    }

    return dominantShare(leadingWords) >= minShare || dominantShare(trailingWords) >= minShare;
}

export function looksLikeSingleDomain(functions: any[], minShare: number): boolean {
    return looksLikeSingleDomainByNames(
        functions.map((fn) => functionNameWords(fn).join(' ')),
        minShare
    );
}
