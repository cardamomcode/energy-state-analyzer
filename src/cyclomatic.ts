import * as vscode from 'vscode';
import { ComplexityHotspot, EnergyViolation, VIOLATION_TYPE, SEVERITY } from './types';

const DECISION_NODE_TYPES = [
    'if_statement', 'elif_clause', 'while_statement', 'for_statement',
    'except_clause', 'and', 'or', 'conditional_expression'
];

// Cyclomatic complexity: counts independent paths through a function.
// Every decision point (if/loop/except/boolean operator/ternary) adds 1,
// regardless of how deeply it is nested.
export function calculateCyclomaticComplexity(functionNode: any): number {
    let complexity = 1; // Base complexity

    function countDecisionPoints(node: any) {
        if (DECISION_NODE_TYPES.includes(node.type)) {
            complexity++;
        }

        for (const child of node.children) {
            countDecisionPoints(child);
        }
    }

    countDecisionPoints(functionNode);
    return complexity;
}

// Locates every decision point in a function and weights it by nesting
// depth, so callers can render a per-line heatmap showing where the
// complexity actually piles up (the metric itself stays flat/unweighted).
export function findCyclomaticHotspots(functionNode: any, document: vscode.TextDocument): ComplexityHotspot[] {
    const hotspots: ComplexityHotspot[] = [];

    function walk(node: any, depth: number) {
        let nextDepth = depth;
        if (DECISION_NODE_TYPES.includes(node.type)) {
            const line = document.positionAt(node.startIndex).line;
            hotspots.push({ line, weight: 1 + depth });
            nextDepth = depth + 1;
        }

        for (const child of node.children) {
            walk(child, nextDepth);
        }
    }

    walk(functionNode, 0);
    return hotspots;
}

export interface CyclomaticThresholds {
    mediumThreshold: number;
    highThreshold: number;
}

export const DEFAULT_CYCLOMATIC_THRESHOLDS: CyclomaticThresholds = {
    mediumThreshold: 10,
    highThreshold: 15
};

export function analyzeFunctionComplexity(
    tree: any,
    document: vscode.TextDocument,
    thresholds: CyclomaticThresholds = DEFAULT_CYCLOMATIC_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (node.type === 'function_definition') {
            const complexity = calculateCyclomaticComplexity(node);
            if (complexity > thresholds.mediumThreshold) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: VIOLATION_TYPE.COMPLEXITY,
                    severity: complexity > thresholds.highThreshold ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                    message: `High cyclomatic complexity: ${complexity}. Consider breaking down this function.`,
                    hotspots: findCyclomaticHotspots(node, document)
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
