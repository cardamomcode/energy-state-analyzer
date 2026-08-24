import { ComplexityHotspot, EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// Cognitive complexity (SonarSource): unlike cyclomatic complexity, every
// decision point is weighted by how deeply it is nested, and early-return
// guard clauses are not penalized. This tracks how hard code is to *read*,
// not just how many paths it has.
//
// Simplifications vs. the full SonarSource spec (acceptable for a first pass):
// - `for`/`while` `else` clauses (where a grammar has them) are scored like
//   `if`/`else`, even though they aren't really a decision point.
// - Boolean operator chain merging ("a and b and c" = one increment) only
//   checks the immediate parent's operator, not the full chain direction.
// - Recursive calls to the enclosing function are not specially detected.
// - match/switch-like constructs and try/except are scored once as a whole,
//   not per-case — see each LanguageAdapter for exact node-type mapping.
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

    function walkNested(node: any, nesting: number) {
        for (const child of node.children) {
            walk(child, language.entersNestedScope(child) ? nesting + 1 : nesting);
        }
    }

    function walk(node: any, nesting: number) {
        const booleanOperator = language.getBooleanOperator(node);
        if (booleanOperator) {
            const parentOperator = language.getBooleanOperator(node.parent);
            if (parentOperator !== booleanOperator) {
                add(node, 1);
            }
            for (const child of node.children) {
                walk(child, nesting);
            }
            return;
        }

        if (language.cognitiveNestedDecisionTypes.includes(node.type)) {
            add(node, 1 + nesting);
            walkNested(node, nesting);
            return;
        }

        if (node.type === nodeTypes.elseClause) {
            add(node, 1); // flat: no extra nesting increment for the else itself
            walkNested(node, nesting);
            return;
        }

        if (node.type === nodeTypes.conditionalExpression) { // ternary: "a if cond else b"
            add(node, 1 + nesting);
            for (const child of node.children) {
                walk(child, nesting + 1);
            }
            return;
        }

        if (language.isFunctionDefinition(node)) {
            // A nested named function/method is scored as its own separate
            // violation by analyzeCognitiveComplexity's traversal, so only the
            // structural nesting increment counts here — walking into its body
            // too would double-count everything inside it.
            add(node, 1 + nesting);
            return;
        }

        if (node.type === nodeTypes.lambda) {
            // Lambdas aren't analyzed as their own function (see LanguageAdapter
            // docs), so their body's complexity belongs to the enclosing function.
            add(node, 1 + nesting);
            walkNested(node, nesting);
            return;
        }

        for (const child of node.children || []) {
            walk(child, nesting);
        }
    }

    for (const child of functionNode.children) {
        walk(child, 0);
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
        if (language.isFunctionDefinition(node)) {
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
