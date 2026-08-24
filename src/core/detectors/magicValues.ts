import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

export interface MagicValuesOptions {
    enabled: boolean;
}

export const DEFAULT_MAGIC_VALUES_OPTIONS: MagicValuesOptions = { enabled: true };

// The "Magic Numbers/Strings" detector
export function analyzeMagicValues(
    tree: any,
    positions: PositionLookup,
    language: LanguageAdapter,
    options: MagicValuesOptions = DEFAULT_MAGIC_VALUES_OPTIONS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    if (!options.enabled) {
        return violations;
    }
    const { nodeTypes } = language;

    function isDocstring(node: any): boolean {
        // decision: treats any standalone string statement as documentation, not a value — covers module/function/class docstrings and PEP 257-style attribute docstrings following a field (e.g. `code: int` then `"""..."""`)
        return node.parent?.type === nodeTypes.expressionStatement;
    }

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

    function traverse(node: any) {
        // Flag suspicious numeric literals
        if (node.type === nodeTypes.integerLiteral || node.type === nodeTypes.floatLiteral) {
            const value = parseInt(node.text) || parseFloat(node.text);
            // decision: exempts 0, 1, 100, and 1000 from magic-number flagging — these appear constantly as loop bounds, percentages, and unit conversions without carrying hidden meaning worth naming
            const isSignificant = value > 1 && value !== 100 && value !== 1000; // Allow common values

            if (isSignificant && !isInConstantContext(node)) {
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

        // Flag suspicious string literals (potential config/messages)
        if (node.type === nodeTypes.stringLiteral && node.text.length > 15 && !isDocstring(node)) {
            const content = node.text.slice(1, -1); // Remove quotes
            // assumption: a string containing a space plus one of "error"/"invalid"/"not found" is a user-facing message worth extracting — narrow on purpose to avoid flagging arbitrary prose strings
            const looksLikeMessage = content.includes(' ') && (content.includes('error') || content.includes('invalid') || content.includes('not found'));

            if (looksLikeMessage) {
                const position = positions.toPosition(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.column,
                    type: VIOLATION_TYPE.MAGIC,
                    severity: SEVERITY.LOW,
                    message: `Magic string: Consider extracting error messages to constants.`
                });
            }
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return violations;
}
