import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

export interface MagicNumberOptions {
    enabled: boolean;
    // Numeric literals that never carry hidden meaning worth naming (loop
    // bounds, unit steps) — checked against the literal's signed value.
    allowlist: number[];
}

export const DEFAULT_MAGIC_NUMBER_OPTIONS: MagicNumberOptions = {
    enabled: true,
    allowlist: [0, 1, -1, 2]
};

// The "Magic Number" detector: numbers don't get an interpolation-style escape hatch the way
// strings do, so this stays broad (any significant literal outside a named binding) and leans
// on the allowlist plus a few structural exemptions (index position, default parameter value)
// to keep it from flagging the common idioms where a bare number isn't actually magic.
// decision: splits on non-alphanumeric separators and camelCase boundaries rather than a plain
// substring match on "test" - a substring check would misflag names like "latest.ts" (ends with
// the literal characters "test") as test files
function splitIntoWords(text: string): string[] {
    return text
        .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
        .split(/[^a-zA-Z0-9]+/)
        .filter(Boolean);
}

// decision: test files are exempt from the magic-number check specifically (not from other
// detectors) - tests still need to be readable/reasoned-about, but literal test inputs/expected
// values (e.g. `assert.equal(result, 42)`) are inherently self-contained and naming them adds
// noise rather than clarity
function isTestFile(fileName: string): boolean {
    const segments = fileName.replace(/\\/g, '/').split('/').filter(Boolean);
    if (segments.some((segment) => /^tests?$/i.test(segment))) {
        return true;
    }
    const base = segments[segments.length - 1] ?? '';
    const stem = base.replace(/\.[^./]+$/, '');
    const words = splitIntoWords(stem);
    if (words.length === 0) {
        return false;
    }
    return words[0].toLowerCase() === 'test' || words[words.length - 1].toLowerCase() === 'test';
}

export function analyzeMagicNumbers(
    tree: any,
    positions: PositionLookup,
    language: LanguageAdapter,
    fileName: string,
    options: MagicNumberOptions = DEFAULT_MAGIC_NUMBER_OPTIONS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    if (!options.enabled || isTestFile(fileName)) {
        return violations;
    }
    const { nodeTypes } = language;

    function isInConstantContext(node: any): boolean {
        // decision: uses a heuristic (assignment at module level, optionally wrapped in `export`) rather than resolving actual scope — cheap to check per-node and sufficient because idiomatic module-level constants are the common case this is meant to exempt
        let parent = node.parent;
        while (parent) {
            if (parent.type === nodeTypes.assignment) {
                // decision: F# has no separate node type for "function definition" vs "plain
                // value binding" (both are function_or_value_defn), so a literal deep inside a
                // function's body would otherwise match this ancestor and be wrongly treated as
                // the function's own "named constant" value. Once the nearest assignment-shaped
                // ancestor is actually a function, the literal is computed logic, not a binding.
                if (language.isFunctionDefinition(parent)) {
                    return false;
                }
                // decision: checked before the module-scope walk below, and independent of it —
                // an explicit compile-time-constant marker (Kotlin's `const val`) is valid at any
                // nesting depth (companion object, object declaration, etc.), unlike the
                // module-scope heuristic that walk relies on for languages with no such marker
                if (language.isExplicitConstant(parent)) {
                    return true;
                }
                // decision: Python wraps every top-level `name = value` in an
                // `expression_statement` between the assignment and the module root (unlike
                // TS's `lexical_declaration`, which sits directly under `program`) — unwrap it
                // before checking against module/export so `MAX_RETRIES = 5` at module scope
                // is still recognized as a constant binding.
                let grandparent = parent.parent;
                if (grandparent?.type === nodeTypes.expressionStatement) {
                    grandparent = grandparent.parent;
                }
                if (grandparent?.type === nodeTypes.module) {
                    // decision: F#'s `declaration_expression` node type is reused both for the
                    // true module root and for wrapping a nested `let` binding's continuation
                    // inside a function body (there's no separate node type for "this let is
                    // the function's actual last expression" vs "this let is module-level") — so
                    // matching nodeTypes.module here isn't enough on its own; this also has to
                    // rule out an enclosing function definition somewhere further up
                    return !hasEnclosingFunction(parent);
                }
                if (
                    nodeTypes.exportStatement &&
                    grandparent?.type === nodeTypes.exportStatement &&
                    grandparent.parent?.type === nodeTypes.module
                ) {
                    return true;
                }
            }
            parent = parent.parent;
        }
        return false;
    }

    function hasEnclosingFunction(assignmentNode: any): boolean {
        let ancestor = assignmentNode.parent;
        while (ancestor) {
            if (language.isFunctionDefinition(ancestor)) {
                return true;
            }
            ancestor = ancestor.parent;
        }
        return false;
    }

    function isIndexPosition(node: any): boolean {
        return !!node.parent && language.subscriptNodeTypes.includes(node.parent.type);
    }

    // decision: a plain `-` unary parent isn't its own node type across every grammar, so this
    // checks the structural shape (a 2-child parent whose first child is a literal `-` token)
    // instead of adding another per-language adapter hook for something this mechanical
    function signedValue(node: any, rawValue: number): number {
        const parent = node.parent;
        const isNegated = parent?.children?.length === 2 && parent.children[0]?.text === '-';
        return isNegated ? -rawValue : rawValue;
    }

    function traverse(node: any) {
        if (node.type === nodeTypes.integerLiteral || node.type === nodeTypes.floatLiteral) {
            // decision: always parseFloat, even for integerLiteral nodes — TS has no separate
            // floatLiteral node type (both map to nodeTypes.integerLiteral), so parseInt would
            // silently truncate a literal like `1.08` down to `1` and match the allowlist
            const rawValue = parseFloat(node.text);
            // decision: skips non-numeric text outright rather than falling through to the
            // allowlist/exemption checks — F#'s float grammar nests a `float`-typed fragment
            // node (digits/'.'/digits) inside its own `float`-typed literal, and TS's
            // `predefined_type` keyword token for the `number` annotation shares nodeTypes'
            // 'number' node type with the actual numeric-literal node; neither is a real value
            if (!Number.isNaN(rawValue)) {
                const value = signedValue(node, rawValue);

                const isExempt =
                    options.allowlist.includes(value) ||
                    isInConstantContext(node) ||
                    isIndexPosition(node) ||
                    language.isDefaultParameterValue(node);

                if (!isExempt) {
                    const position = positions.toPosition(node.startIndex);
                    violations.push({
                        line: position.line,
                        column: position.column,
                        type: VIOLATION_TYPE.MAGIC,
                        severity: SEVERITY.LOW,
                        message: `Magic number: ${node.text}. Consider extracting to a named constant.`
                    });
                }
            }
            // A literal's own children (if any) are fragments of the same value, not
            // separate literals — never descend into them.
            return;
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return violations;
}
