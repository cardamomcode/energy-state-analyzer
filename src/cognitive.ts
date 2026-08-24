import * as vscode from 'vscode';
import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from './types';

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
export function calculateCognitiveComplexity(functionNode: any): number {
    let score = 0;

    function getBooleanOperator(node: any): string | null {
        const opToken = node.children?.find((c: any) => c.type === 'and' || c.type === 'or');
        return opToken ? opToken.type : null;
    }

    function walk(node: any, nesting: number) {
        switch (node.type) {
            case 'if_statement': {
                score += 1 + nesting;
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
                score += 1 + nesting;
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'else_clause': {
                score += 1; // flat: no extra nesting increment for the else itself
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'for_statement':
            case 'while_statement': {
                score += 1 + nesting;
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'except_clause': {
                score += 1 + nesting;
                for (const child of node.children) {
                    walk(child, child.type === 'block' ? nesting + 1 : nesting);
                }
                return;
            }
            case 'conditional_expression': { // ternary: "a if cond else b"
                score += 1 + nesting;
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
                    score += 1;
                }
                for (const child of node.children) {
                    walk(child, nesting);
                }
                return;
            }
            case 'lambda':
            case 'function_definition': { // nested function/lambda adds structural nesting
                score += 1 + nesting;
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

export function analyzeCognitiveComplexity(tree: any, document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (node.type === 'function_definition') {
            const complexity = calculateCognitiveComplexity(node);
            if (complexity > 15) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: VIOLATION_TYPE.COGNITIVE,
                    severity: complexity > 25 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                    message: `High cognitive complexity: ${complexity}. This function is hard to read; consider flattening nesting or extracting functions.`
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
