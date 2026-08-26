import { ComplexityHotspot, EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// Cyclomatic complexity: counts independent paths through a function.
// Every decision point (if/loop/except/boolean operator/ternary) adds 1,
// regardless of how deeply it is nested.
export function calculateCyclomaticComplexity(functionNode: any, language: LanguageAdapter): number {
    let complexity = 1; // Base complexity

    function countDecisionPoints(node: any, isRoot: boolean) {
        if (
            language.decisionNodeTypes.includes(node.type) ||
            language.getBooleanOperator(node) !== null ||
            language.isTryElseClause(node)
        ) {
            complexity++;
        }

        // invariant: a nested named function/method's decision points are never counted toward the enclosing function's complexity — it is scored as its own separate violation by analyzeFunctionComplexity's traversal
        if (!isRoot && language.isFunctionDefinition(node)) {
            return;
        }

        for (const child of node.children) {
            countDecisionPoints(child, false);
        }
    }

    countDecisionPoints(functionNode, true);
    return complexity;
}

// Locates every decision point in a function and weights it by nesting
// depth, so callers can render a per-line heatmap showing where the
// complexity actually piles up (the metric itself stays flat/unweighted).
export function findCyclomaticHotspots(
    functionNode: any,
    positions: PositionLookup,
    language: LanguageAdapter
): ComplexityHotspot[] {
    const hotspots: ComplexityHotspot[] = [];

    function walk(node: any, depth: number, isRoot: boolean) {
        let nextDepth = depth;
        if (
            language.decisionNodeTypes.includes(node.type) ||
            language.getBooleanOperator(node) !== null ||
            language.isTryElseClause(node)
        ) {
            const line = positions.toPosition(node.startIndex).line;
            hotspots.push({ line, weight: 1 + depth });
            nextDepth = depth + 1;
        }

        // invariant: mirrors calculateCyclomaticComplexity's traversal exactly — a nested named function/method is hotspotted separately as its own violation, never folded into this one
        if (!isRoot && language.isFunctionDefinition(node)) {
            return;
        }

        for (const child of node.children) {
            walk(child, nextDepth, false);
        }
    }

    walk(functionNode, 0, true);
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
    positions: PositionLookup,
    language: LanguageAdapter,
    thresholds: CyclomaticThresholds = DEFAULT_CYCLOMATIC_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (language.isFunctionDefinition(node)) {
            const complexity = calculateCyclomaticComplexity(node, language);
            if (complexity > thresholds.mediumThreshold) {
                const position = positions.toPosition(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.column,
                    type: VIOLATION_TYPE.COMPLEXITY,
                    severity: complexity > thresholds.highThreshold ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                    message: `High cyclomatic complexity: ${complexity}. Consider breaking down this function.`,
                    hotspots: findCyclomaticHotspots(node, positions, language)
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
