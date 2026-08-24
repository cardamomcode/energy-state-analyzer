import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// The "Magic Numbers/Strings" detector
export function analyzeMagicValues(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const { nodeTypes } = language;

    function isDocstring(node: any): boolean {
        // A standalone string statement is documentation, not a value, whether it's a
        // module/function/class docstring or a PEP 257-style attribute docstring
        // following a field (e.g. `code: int` then `"""..."""`).
        return node.parent?.type === nodeTypes.expressionStatement;
    }

    function isInConstantContext(node: any): boolean {
        // Simple heuristic: check if parent is an assignment at module level,
        // optionally wrapped in an `export` statement (e.g. TS/JS
        // `export const x = ...`), which sits between the assignment and module.
        let parent = node.parent;
        while (parent) {
            if (parent.type === nodeTypes.assignment) {
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
