import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

export function analyzeNesting(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any, depth: number = 0) {
        if (language.nestingControlTypes.includes(node.type)) {
            // decision: flags nesting past depth 3 (medium) and depth 5 (high) — three levels of if/for/while/with is the point where tracking active conditions starts to strain working memory
            if (depth > 3) {
                const position = positions.toPosition(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.column,
                    type: VIOLATION_TYPE.NESTING,
                    severity: depth > 5 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
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
