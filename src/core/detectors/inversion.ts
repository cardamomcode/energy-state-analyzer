import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// The "Inversion Opportunity" detector - finds patterns that could benefit from early returns
export function analyzeInversionOpportunities(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const { nodeTypes } = language;

    function traverse(node: any) {
        if (language.isFunctionDefinition(node)) {
            analyzeFunction(node);
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    function analyzeFunction(functionNode: any) {
        // Find the function body
        const body = functionNode.children.find((child: any) => child.type === nodeTypes.block);
        if (!body) { return; }

        // Look for patterns that could benefit from inversion
        //
        // decision: filters on isNamed, not just a non-comment/non-blank text check - grammars
        // with an explicit block wrapper (e.g. TS's statement_block) include the literal '{'/'}'
        // tokens in .children, and those pass a text?.trim() check same as a real statement would.
        // Without this, `statements[0]` on such grammars is always the '{' token, never the
        // function's actual first statement, so Pattern 1 below can never match.
        const statements = body.children.filter((child: any) =>
            child.isNamed && child.type !== nodeTypes.comment && child.text?.trim()
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

                    // decision: flags only when the if-body spans more than half the function — below that, the if is a fragment rather than the function's dominant structure
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
        // decision: caps the validation-chain walk at 4 levels — deep chains beyond that are already caught by analyzeNestedIfs, so this loop only needs to bound its own work
        let currentNode = body;
        let nestingLevel = 0;
        const validationChecks: any[] = [];

        while (currentNode && nestingLevel < 4) {
            // decision: matches against language.nestingControlTypes rather than the
            // nodeTypes.ifStatement/forStatement/whileStatement trio directly — TypeScript's
            // `for...of`/`for...in` parses as its own for_in_statement node, distinct from
            // nodeTypes.forStatement's plain `for_statement`, and nestingControlTypes is the
            // per-language set that already accounts for that (bug found by running this
            // detector on its own source: a sibling for-of loop went unseen by the old filter,
            // making its lone if-sibling look like the only statement in this body and get
            // mistaken for a validation-chain step)
            const statements = currentNode.children?.filter((child: any) =>
                language.nestingControlTypes.includes(child.type)
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

        // decision: suggests guard clauses starting at 2 chained validation checks — a single nested if is normal control flow, not yet a pattern worth restructuring
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
            // invariant: never descends into a nested function/method's body — that function is
            // walked as its own separate analyzeFunction call once traverse() reaches it, so
            // counting its if-nesting here too would both double-report the same location and
            // misattribute nesting depth to the wrong enclosing function (mirrors the same rule
            // in cognitive.ts's walk())
            if (language.isFunctionDefinition(node)) {
                return;
            }

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

        // decision: flags if-nesting at 3+ levels here, one level below analyzeNesting's depth>3 threshold — this detector targets flattenable if-chains specifically, so it can fire earlier than the general nesting detector
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
