import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// The "Inversion Opportunity" detector - finds patterns that could benefit from early returns
export function analyzeInversionOpportunities(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const { nodeTypes } = language;

    function traverse(node: any) {
        if (language.functionDefinitionTypes.includes(node.type)) {
            analyzeFunction(node);
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    function analyzeFunction(functionNode: any) {
        // Find the function body
        const body = functionNode.children.find((child: any) => child.type === nodeTypes.block);
        if (!body) return;

        // Look for patterns that could benefit from inversion
        const statements = body.children.filter((child: any) =>
            child.type !== nodeTypes.comment && child.text?.trim()
        );

        // Pattern 1: Single large if-statement dominating the function
        if (statements.length >= 1) {
            const firstStatement = statements[0];
            if (firstStatement.type === nodeTypes.ifStatement) {
                const ifBody = firstStatement.children.find((child: any) => child.type === nodeTypes.block);
                if (ifBody && ifBody.children.length > 2) {
                    // Check if this if-statement contains most of the function logic
                    const totalLines = functionNode.endIndex - functionNode.startIndex;
                    const ifLines = ifBody.endIndex - ifBody.startIndex;
                    const ratio = ifLines / totalLines;

                    if (ratio > 0.5) {
                        const position = positions.toPosition(firstStatement.startIndex);
                        violations.push({
                            line: position.line,
                            column: position.column,
                            type: VIOLATION_TYPE.INVERSION,
                            severity: SEVERITY.MEDIUM,
                            message: 'Consider inverting this condition and using early return for cleaner flow.'
                        });
                    }
                }
            }
        }

        // Pattern 2: Nested validation checks that could be guard clauses
        analyzeNestedValidation(body);

        // Pattern 3: Multiple nested if-statements that could be flattened
        analyzeNestedIfs(body);
    }

    function analyzeNestedValidation(body: any) {
        // Look for patterns like: if (valid) { if (moreValid) { if (evenMoreValid) { ... } } }
        let currentNode = body;
        let nestingLevel = 0;
        const validationChecks: any[] = [];

        while (currentNode && nestingLevel < 4) {
            const statements = currentNode.children?.filter((child: any) =>
                child.type === nodeTypes.ifStatement || child.type === nodeTypes.forStatement || child.type === nodeTypes.whileStatement
            ) || [];

            if (statements.length === 1 && statements[0].type === nodeTypes.ifStatement) {
                const ifStatement = statements[0];
                validationChecks.push(ifStatement);

                // Check if this looks like a validation (no else clause, simple condition)
                const hasElse = ifStatement.children.some((child: any) => child.type === nodeTypes.elseClause);
                if (!hasElse) {
                    const ifBody = ifStatement.children.find((child: any) => child.type === nodeTypes.block);
                    currentNode = ifBody;
                    nestingLevel++;
                } else {
                    break;
                }
            } else {
                break;
            }
        }

        // If we found 2+ validation checks, suggest guard clauses
        if (validationChecks.length >= 2) {
            const firstCheck = validationChecks[0];
            const position = positions.toPosition(firstCheck.startIndex);
            violations.push({
                line: position.line,
                column: position.column,
                type: VIOLATION_TYPE.INVERSION,
                severity: SEVERITY.MEDIUM,
                message: `Found ${validationChecks.length} nested validation checks. Consider using guard clauses with early returns.`
            });
        }
    }

    function analyzeNestedIfs(body: any) {
        // Count deeply nested if statements that could be flattened
        let maxNesting = 0;
        let nestedIfLocation: any = null;

        function countNesting(node: any, currentDepth: number = 0) {
            if (node.type === nodeTypes.ifStatement) {
                if (currentDepth > maxNesting) {
                    maxNesting = currentDepth;
                    nestedIfLocation = node;
                }
                currentDepth++;
            }

            for (const child of node.children || []) {
                countNesting(child, currentDepth);
            }
        }

        countNesting(body);

        // Flag functions with 3+ levels of if nesting
        if (maxNesting >= 3 && nestedIfLocation) {
            const position = positions.toPosition(nestedIfLocation.startIndex);
            violations.push({
                line: position.line,
                column: position.column,
                type: VIOLATION_TYPE.INVERSION,
                severity: SEVERITY.MEDIUM,
                message: `Deep if-nesting (${maxNesting} levels). Consider inverting conditions or extracting functions.`
            });
        }
    }

    traverse(tree.rootNode);
    return violations;
}
