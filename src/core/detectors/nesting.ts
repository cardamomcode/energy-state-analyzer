import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

export interface NestingThresholds {
    mediumThreshold: number;
    highThreshold: number;
}

// decision: default medium threshold of 3 is the point where tracking active conditions starts to strain working memory; high threshold of 5 escalates severity for the deepest offenders
export const DEFAULT_NESTING_THRESHOLDS: NestingThresholds = {
    mediumThreshold: 3,
    highThreshold: 5
};

export function analyzeNesting(
    tree: any,
    positions: PositionLookup,
    language: LanguageAdapter,
    thresholds: NestingThresholds = DEFAULT_NESTING_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any, depth: number = 0) {
        if (language.nestingControlTypes.includes(node.type)) {
            if (depth > thresholds.mediumThreshold) {
                const position = positions.toPosition(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.column,
                    type: VIOLATION_TYPE.NESTING,
                    severity: depth > thresholds.highThreshold ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                    message: `Excessive nesting depth: ${depth}. Consider extracting.`
                });
            }
            depth++;
        }

        for (const child of node.children) {
            traverse(child, depth);
        }
    }

    traverse(tree.rootNode);
    return violations;
}
