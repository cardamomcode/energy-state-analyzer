import { ComplexityHotspot, EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

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
export function calculateCognitiveComplexity(
    functionNode: any,
    language: LanguageAdapter,
    onContribution?: (node: any, amount: number) => void
): number {
    let score = 0;
    const { nodeTypes } = language;

    function add(node: any, amount: number) {
        score += amount;
        onContribution?.(node, amount);
    }

    function getBooleanOperator(node: any): string | null {
        const opToken = node.children?.find((c: any) => c.type === nodeTypes.booleanAnd || c.type === nodeTypes.booleanOr);
        return opToken ? opToken.type : null;
    }

    function walk(node: any, nesting: number) {
        switch (node.type) {
            case nodeTypes.ifStatement: {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === nodeTypes.block ? nesting + 1 : nesting);
                }
                return;
            }
            case nodeTypes.elifClause: {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === nodeTypes.block ? nesting + 1 : nesting);
                }
                return;
            }
            case nodeTypes.elseClause: {
                add(node, 1); // flat: no extra nesting increment for the else itself
                for (const child of node.children) {
                    walk(child, child.type === nodeTypes.block ? nesting + 1 : nesting);
                }
                return;
            }
            case nodeTypes.forStatement:
            case nodeTypes.whileStatement: {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === nodeTypes.block ? nesting + 1 : nesting);
                }
                return;
            }
            case nodeTypes.exceptClause: {
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === nodeTypes.block ? nesting + 1 : nesting);
                }
                return;
            }
            case nodeTypes.conditionalExpression: { // ternary: "a if cond else b"
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, nesting + 1);
                }
                return;
            }
            case nodeTypes.booleanOperator: { // and / or
                const operator = getBooleanOperator(node);
                const parentOperator = node.parent?.type === nodeTypes.booleanOperator ? getBooleanOperator(node.parent) : null;
                const isChainContinuation = parentOperator !== null && parentOperator === operator;
                if (!isChainContinuation) {
                    add(node, 1);
                }
                for (const child of node.children) {
                    walk(child, nesting);
                }
                return;
            }
            case nodeTypes.lambda:
            case nodeTypes.functionDefinition: { // nested function/lambda adds structural nesting
                add(node, 1 + nesting);
                for (const child of node.children) {
                    walk(child, child.type === nodeTypes.block ? nesting + 1 : nesting);
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

    const body = functionNode.children.find((child: any) => child.type === nodeTypes.block);
    if (body) {
        walk(body, 0);
    }

    return score;
}

// Re-runs the same walk used for scoring, but records where each point of
// score comes from so callers can render a per-line heatmap across the
// function body instead of a single flat highlight.
export function findCognitiveHotspots(functionNode: any, positions: PositionLookup, language: LanguageAdapter): ComplexityHotspot[] {
    const hotspots: ComplexityHotspot[] = [];
    calculateCognitiveComplexity(functionNode, language, (node, amount) => {
        hotspots.push({ line: positions.toPosition(node.startIndex).line, weight: amount });
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
    positions: PositionLookup,
    language: LanguageAdapter,
    thresholds: CognitiveThresholds = DEFAULT_COGNITIVE_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (node.type === language.nodeTypes.functionDefinition) {
            const complexity = calculateCognitiveComplexity(node, language);
            if (complexity > thresholds.mediumThreshold) {
                const position = positions.toPosition(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.column,
                    type: VIOLATION_TYPE.COGNITIVE,
                    severity: complexity > thresholds.highThreshold ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                    message: `High cognitive complexity: ${complexity}. This function is hard to read; consider flattening nesting or extracting functions.`,
                    hotspots: findCognitiveHotspots(node, positions, language)
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
