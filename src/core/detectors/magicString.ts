import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

export interface MagicStringOptions {
    enabled: boolean;
    // Only flag a string once it recurs at a decision point at least this many times across
    // the file — mirrors Sonar's S1192, and cuts single-use false positives further.
    minDuplicates: number;
    // Literal contents that are never worth naming regardless of how often they recur.
    allowlist: string[];
}

export const DEFAULT_MAGIC_STRING_OPTIONS: MagicStringOptions = {
    enabled: true,
    minDuplicates: 2,
    allowlist: ['', 'utf-8', '__main__']
};

// decision: a call whose callee text matches this is treated as "this string is a
// human-facing message, not an enum-like token" — narrow on purpose (see magicValues.ts's
// prior heuristic, which this replaces) to avoid exempting every call in the file
const LOGGING_OR_EXCEPTION_CALLEE = /print|log|logger|logging|exception|error|panic|warn|assert/i;

// The "Magic String" detector: unlike numbers, strings get a narrow scope — only literals
// standing at a decision point (compared, checked for membership, or used as a dict/object
// key) are candidates, since that's where an unnamed string actually risks a silent typo. Any
// other string (a message, a docstring, an interpolated template) is left alone entirely.
export function analyzeMagicStrings(
    tree: any,
    positions: PositionLookup,
    language: LanguageAdapter,
    options: MagicStringOptions = DEFAULT_MAGIC_STRING_OPTIONS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    if (!options.enabled) {
        return violations;
    }
    const { nodeTypes } = language;

    function isDocstring(node: any): boolean {
        return node.parent?.type === nodeTypes.expressionStatement;
    }

    function stripQuotes(text: string): string {
        return text.length >= 2 ? text.slice(1, -1) : text;
    }

    function isEqualityComparisonOperand(node: any): boolean {
        // decision: also checks the grandparent, not just the parent — F#'s grammar wraps a
        // literal operand in an intermediate `const` node before it reaches the
        // `infix_expression`, so the comparison node itself sits one level further up than in
        // Python/TS. Checking the parent alone would silently drop every F# string comparison.
        const candidates = [node.parent, node.parent?.parent].filter(Boolean);
        // decision: compares node identity by `.id`, not `===` — web-tree-sitter mints a fresh
        // JS wrapper object on every `.children`/`.parent` access, so two accessors that reach
        // the same underlying tree node are not reference-equal even though `.id` matches
        return candidates.some(candidate =>
            language.getEqualityComparisons(candidate).some(({ left, right }) => left.id === node.id || right.id === node.id));
    }

    function isMembershipOperand(node: any, content: string): boolean {
        const container = node.parent?.parent;
        if (!container) {
            return false;
        }
        return language.getMembershipComparisons(container).some(({ values }) => values.includes(content));
    }

    function isKeyOrIndexPosition(node: any): boolean {
        return !!node.parent && language.subscriptNodeTypes.includes(node.parent.type);
    }

    function isLoggingOrExceptionArgument(node: any): boolean {
        const argList = node.parent;
        if (!argList || !language.callArgumentListTypes.includes(argList.type)) {
            return false;
        }
        const callNode = argList.parent;
        if (!callNode || !language.callNodeTypes.includes(callNode.type)) {
            return false;
        }
        return LOGGING_OR_EXCEPTION_CALLEE.test(language.getCallCalleeText(callNode));
    }

    function isDecisionPoint(node: any, content: string): boolean {
        return isEqualityComparisonOperand(node) || isMembershipOperand(node, content) || isKeyOrIndexPosition(node);
    }

    function isExempt(node: any, content: string): boolean {
        return isDocstring(node)
            || language.isFormattedOrInterpolatedString(node)
            || isLoggingOrExceptionArgument(node)
            || options.allowlist.includes(content)
            || content.length <= 1;
    }

    interface Candidate { node: any; content: string; }
    const candidates: Candidate[] = [];

    function traverse(node: any) {
        if (node.type === nodeTypes.stringLiteral) {
            const content = stripQuotes(node.text);
            if (isDecisionPoint(node, content) && !isExempt(node, content)) {
                candidates.push({ node, content });
            }
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);

    const byContent = new Map<string, Candidate[]>();
    for (const candidate of candidates) {
        const group = byContent.get(candidate.content) ?? [];
        group.push(candidate);
        byContent.set(candidate.content, group);
    }

    for (const [content, group] of byContent) {
        if (group.length < options.minDuplicates) {
            continue;
        }
        const first = group[0].node;
        const position = positions.toPosition(first.startIndex);
        violations.push({
            line: position.line,
            column: position.column,
            type: VIOLATION_TYPE.MAGIC,
            severity: SEVERITY.LOW,
            message: `Magic string: "${content}" is compared/keyed against directly ${group.length} time(s). Consider extracting to a named constant or enum.`
        });
    }

    return violations;
}
