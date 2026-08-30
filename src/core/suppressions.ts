import { EnergyViolation, VIOLATION_TYPE } from '../types';

// decision: parsed by scanning raw source lines with a regex instead of walking the tree-sitter
// AST — the comment node type differs per grammar (see LanguageAdapter.nodeTypes.comment) but the
// esa-ignore marker text itself is identical across languages, so a text scan avoids needing a
// per-language comment-node lookup just to find `//` and `#` comments.
// decision: anchored with `\s*$` so the marker must be a complete token — bare at end-of-line or
// followed by `:` + type list — not prose that merely mentions "esa-ignore" (a file whose own
// comment starts "// esa-ignore ..." would otherwise parse as an unused bare directive).
const DIRECTIVE_PATTERN = /(\/\/|#)\s*esa-ignore(-file)?(?::\s*([\w,\s-]+))?\s*$/;

const KNOWN_TYPES: Set<string> = new Set(Object.values(VIOLATION_TYPE));

export interface Suppression {
    // 0-indexed line the directive comment itself sits on (matches EnergyViolation.line).
    line: number;
    column: number;
    scope: 'line' | 'file';
    types: string[] | 'all';
    unknownTypes: string[];
    // Whether the directive is the only content on its line — see isCoveredBy below.
    standalone: boolean;
}

// decision: an explicit but entirely-unrecognized type list (e.g. a typo'd `esa-ignore:
// nseting`) resolves to an empty list, not a fallback to 'all' — falling back to 'all' would
// make a typo silently suppress *more* than intended instead of less, which is the opposite of
// the "keep suppression honest" goal (see the unused-directive note this produces below). Only
// a bare directive with no colon at all means "suppress every type".
function parseTypeList(raw: string | undefined): { types: string[] | 'all'; unknownTypes: string[] } {
    if (!raw) {
        return { types: 'all', unknownTypes: [] };
    }
    const tokens = raw
        .split(',')
        .map((token) => token.trim())
        .filter(Boolean);
    const types = tokens.filter((token) => KNOWN_TYPES.has(token));
    const unknownTypes = tokens.filter((token) => !KNOWN_TYPES.has(token));
    return { types, unknownTypes };
}

export function parseSuppressions(sourceText: string): Suppression[] {
    const lines = sourceText.split('\n');
    const suppressions: Suppression[] = [];

    lines.forEach((lineText, line) => {
        const match = DIRECTIVE_PATTERN.exec(lineText);
        if (!match) {
            return;
        }
        const { types, unknownTypes } = parseTypeList(match[3]);
        suppressions.push({
            line,
            column: match.index,
            scope: match[2] ? 'file' : 'line',
            types,
            unknownTypes,
            standalone: lineText.slice(0, match.index).trim() === ''
        });
    });

    return suppressions;
}

// decision: a standalone directive (nothing but the comment on its line) also covers the next
// line — lets a suppression read naturally placed above a multi-line construct's header (a
// function signature, an if-condition) instead of forcing it onto an already-long code line.
function coversLine(suppression: Suppression, violationLine: number): boolean {
    if (suppression.scope === 'file') {
        return true;
    }
    if (violationLine === suppression.line) {
        return true;
    }
    return suppression.standalone && violationLine === suppression.line + 1;
}

function matchesType(suppression: Suppression, type: string): boolean {
    return suppression.types === 'all' || suppression.types.includes(type);
}

export interface ApplySuppressionsResult {
    violations: EnergyViolation[];
    suppressionNotes: EnergyViolation[];
}

// decision: returns unused/unknown-type directives as their own low-severity violations (rather
// than silently dropping them) — an esa-ignore that stopped matching anything (the violation was
// fixed, or the type name was mistyped) is exactly the kind of stale suppression that should be
// visible, not a silent no-op that quietly keeps working "by accident".
export function applySuppressions(violations: EnergyViolation[], sourceText: string): ApplySuppressionsResult {
    const suppressions = parseSuppressions(sourceText);
    if (suppressions.length === 0) {
        return { violations, suppressionNotes: [] };
    }

    const suppressedCounts = new Map<Suppression, number>(suppressions.map((s) => [s, 0]));

    const remaining = violations.filter((violation) => {
        const match = suppressions.find((s) => coversLine(s, violation.line) && matchesType(s, violation.type));
        if (!match) {
            return true;
        }
        suppressedCounts.set(match, (suppressedCounts.get(match) ?? 0) + 1);
        return false;
    });

    const suppressionNotes: EnergyViolation[] = [];
    for (const suppression of suppressions) {
        if (suppression.unknownTypes.length > 0) {
            suppressionNotes.push({
                line: suppression.line,
                column: suppression.column,
                type: VIOLATION_TYPE.SUPPRESSION,
                severity: 'low',
                message: `esa-ignore names unknown violation type(s): ${suppression.unknownTypes.join(', ')}.`
            });
        }
        if ((suppressedCounts.get(suppression) ?? 0) === 0) {
            const scopeText = suppression.scope === 'file' ? 'file-wide ' : '';
            suppressionNotes.push({
                line: suppression.line,
                column: suppression.column,
                type: VIOLATION_TYPE.SUPPRESSION,
                severity: 'low',
                message: `Unused ${scopeText}esa-ignore — no matching violation found. Remove it or fix the type list.`
            });
        }
    }

    return { violations: remaining, suppressionNotes };
}
