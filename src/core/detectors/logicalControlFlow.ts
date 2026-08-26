import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// The "Logical Operator as Control Flow" detector: flags `condition && doSomething();` (or
// `condition || fallback();`) used as a standalone statement.
//
// decision: this is legal in every language the LanguageAdapter grammar reports an
// expressionStatement for (Python's bare `and`/`or` expression statement included, not just
// TS/JS's `&&`/`||`) and it already counts toward cyclomatic complexity via
// getBooleanOperator (see cyclomatic.ts) — this detector exists only to name the *readability*
// cost separately: an if hidden as an expression is invisible to anyone skimming for branches,
// and can't grow past a single consequent expression without becoming unreadable.
export function analyzeLogicalControlFlow(
    tree: any,
    positions: PositionLookup,
    language: LanguageAdapter
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const { nodeTypes } = language;

    function traverse(node: any) {
        const operator = language.getBooleanOperator(node);
        if (operator !== null && node.parent?.type === nodeTypes.expressionStatement) {
            const position = positions.toPosition(node.startIndex);
            violations.push({
                line: position.line,
                column: position.column,
                type: VIOLATION_TYPE.LOGICAL_CONTROL_FLOW,
                severity: SEVERITY.LOW,
                message:
                    operator === 'and'
                        ? "If-statement disguised as '&&'. Consider an explicit if-statement instead."
                        : "If-statement disguised as '||'. Consider an explicit if-statement instead."
            });
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return violations;
}
