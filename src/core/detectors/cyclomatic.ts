import { ComplexityHotspot, EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// Cyclomatic complexity: counts independent paths through a function.
// Every decision point (if/loop/except/boolean operator/ternary) adds 1,
// regardless of how deeply it is nested.
export function calculateCyclomaticComplexity(functionNode: any, language: LanguageAdapter): number {
    let complexity = 1; // Base complexity

    function countDecisionPoints(node: any) {
        if (language.decisionNodeTypes.includes(node.type)) {
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
export function findCyclomaticHotspots(functionNode: any, positions: PositionLookup, language: LanguageAdapter): ComplexityHotspot[] {
    const hotspots: ComplexityHotspot[] = [];

    function walk(node: any, depth: number) {
        let nextDepth = depth;
        if (language.decisionNodeTypes.includes(node.type)) {
            const line = positions.toPosition(node.startIndex).line;
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
    positions: PositionLookup,
    language: LanguageAdapter,
    thresholds: CyclomaticThresholds = DEFAULT_CYCLOMATIC_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (node.type === language.nodeTypes.functionDefinition) {
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
