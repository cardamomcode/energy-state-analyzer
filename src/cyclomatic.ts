import * as vscode from 'vscode';
import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from './types';

// Cyclomatic complexity: counts independent paths through a function.
// Every decision point (if/loop/except/boolean operator/ternary) adds 1,
// regardless of how deeply it is nested.
export function calculateCyclomaticComplexity(functionNode: any): number {
    let complexity = 1; // Base complexity

    function countDecisionPoints(node: any) {
        const decisionNodes = [
            'if_statement', 'elif_clause', 'while_statement', 'for_statement',
            'except_clause', 'and', 'or', 'conditional_expression'
        ];

        if (decisionNodes.includes(node.type)) {
            complexity++;
        }

        for (const child of node.children) {
            countDecisionPoints(child);
        }
    }

    countDecisionPoints(functionNode);
    return complexity;
}

export function analyzeFunctionComplexity(tree: any, document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (node.type === 'function_definition') {
            const complexity = calculateCyclomaticComplexity(node);
            if (complexity > 10) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: VIOLATION_TYPE.COMPLEXITY,
                    severity: complexity > 15 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                    message: `High cyclomatic complexity: ${complexity}. Consider breaking down this function.`
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
