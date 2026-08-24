import * as vscode from 'vscode';
import { ComplexityHotspot, EnergyViolation, VIOLATION_TYPE, SEVERITY } from './types';

// Cognitive complexity (SonarSource): unlike cyclomatic complexity, every
// decision point is weighted by how deeply it is nested, and early-return
// guard clauses are not penalized. This tracks how hard code is to *read*,
// not just how many paths it has.
//
// Simplifications vs. the full SonarSource spec (acceptable for a first pass):
// - Python's `else` on `for`/`while` loops is treated the same as `if`/`else`
//   (a flat +1), even though it isn't really a decision point.
// - Boolean operator chain merging ("a and b and c" = one increment) only
//   checks the immediate parent's operator, not the full chain direction.
// - Recursive calls to the enclosing function are not specially detected.
export function calculateCognitiveComplexity(functionNode: any, onContribution?: (node: any, amount: number) => void): number {
    let score = 0;

    function add(node: any, amount: number) {
        score += amount;
        onContribution?.(node, amount);
    }

    function getBooleanOperator(node: any): string | null {
        const opToken = node.children?.find((c: any) => c.type === 'and' || c.type === 'or');
        return opToken ? opToken.type : null;
    }

    function walk(node: any, nesting: number) {
        switch (node.type) {
            case 'if_statement': {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    if (child.type === 'block') {
                        walk(child, nesting + 1);
                    } else {
                        walk(child, nesting);
                    }
                }
                return;
            }
            case 'elif_clause': {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'else_clause': {
                add(node, 1); // flat: no extra nesting increment for the else itself
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'for_statement':
            case 'while_statement': {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'except_clause': {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'conditional_expression': { // ternary: "a if cond else b"
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, nesting + 1);
                }
                return;
            }
            case 'boolean_operator': { // and / or
                const operator = getBooleanOperator(node);
                const parentOperator = node.parent?.type === 'boolean_operator' ? getBooleanOperator(node.parent) : null;
                const isChainContinuation = parentOperator !== null && parentOperator === operator;
                if (!isChainContinuation) {
                    add(node, 1);
                }
                for (const child of node.children) {
                    walk(child, nesting);
                }
                return;
            }
            case 'lambda':
            case 'function_definition': { // nested function/lambda adds structural nesting
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            default: {
                for (const child of node.children || []) {
                    walk(child, nesting);
                }
            }
        }
    }

    const body = functionNode.children.find((child: any) => child.type === 'block');
    if (body) {
        walk(body, 0);
    }

    return score;
}

// Re-runs the same walk used for scoring, but records where each point of
// score comes from so callers can render a per-line heatmap across the
// function body instead of a single flat highlight.
export function findCognitiveHotspots(functionNode: any, document: vscode.TextDocument): ComplexityHotspot[] {
    const hotspots: ComplexityHotspot[] = [];
    calculateCognitiveComplexity(functionNode, (node, amount) => {
        hotspots.push({ line: document.positionAt(node.startIndex).line, weight: amount });
    });
    return hotspots;
}

export interface CognitiveThresholds {
    mediumThreshold: number;
    highThreshold: number;
}

export const DEFAULT_COGNITIVE_THRESHOLDS: CognitiveThresholds = {
    mediumThreshold: 15,
    highThreshold: 25
};

export function analyzeCognitiveComplexity(
    tree: any,
    document: vscode.TextDocument,
    thresholds: CognitiveThresholds = DEFAULT_COGNITIVE_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (node.type === 'function_definition') {
            const complexity = calculateCognitiveComplexity(node);
            if (complexity > thresholds.mediumThreshold) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: VIOLATION_TYPE.COGNITIVE,
                    severity: complexity > thresholds.highThreshold ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                    message: `High cognitive complexity: ${complexity}. This function is hard to read; consider flattening nesting or extracting functions.`,
                    hotspots: findCognitiveHotspots(node, document)
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
