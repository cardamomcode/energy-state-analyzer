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
export function analyzeMagicNumbers(
    tree: any,
    positions: PositionLookup,
    language: LanguageAdapter,
    options: MagicNumberOptions = DEFAULT_MAGIC_NUMBER_OPTIONS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    if (!options.enabled) {
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
                const grandparent = parent.parent;
                if (grandparent?.type === nodeTypes.module) {
                    return true;
                }
                if (nodeTypes.exportStatement && grandparent?.type === nodeTypes.exportStatement
                    && grandparent.parent?.type === nodeTypes.module) {
                    return true;
                }
            }
            parent = parent.parent;
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
            const rawValue = node.type === nodeTypes.integerLiteral ? parseInt(node.text, 10) : parseFloat(node.text);
            // decision: skips non-numeric text outright rather than falling through to the
            // allowlist/exemption checks — F#'s float grammar nests a `float`-typed fragment
            // node (digits/'.'/digits) inside its own `float`-typed literal, and TS's
            // `predefined_type` keyword token for the `number` annotation shares nodeTypes'
            // 'number' node type with the actual numeric-literal node; neither is a real value
            if (!Number.isNaN(rawValue)) {
                const value = signedValue(node, rawValue);

                const isExempt = options.allowlist.includes(value)
                    || isInConstantContext(node)
                    || isIndexPosition(node)
                    || language.isDefaultParameterValue(node);

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
