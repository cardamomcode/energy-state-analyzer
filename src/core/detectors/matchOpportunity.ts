import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

export interface MatchOpportunityThresholds {
    minBranches: number;
}

export const DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS: MatchOpportunityThresholds = {
    minBranches: 3
};

function stripQuotes(text: string): string {
    return text.slice(1, -1);
}

// Walks an if-statement/if-expression node and returns every branch in the
// same chain, in source order: the node itself, then one of three grammar
// shapes for its continuations —
//   - flat elif siblings (Python/F#, via getElseIfBranches)
//   - else if there are none, `else if`-nested if-statements one level
//     inside an else_clause wrapper (TypeScript's shape)
//   - or, for grammars with no else_clause wrapper at all (Kotlin's
//     if_expression), a nested if-statement sitting as a *direct* child of
//     the previous one, with no wrapper node to key off
function collectChainBranches(ifNode: any, language: LanguageAdapter): any[] {
    const branches = [ifNode];

    const flatSiblings = language.getElseIfBranches(ifNode);
    if (flatSiblings.length > 0) {
        branches.push(...flatSiblings);
        return branches;
    }

    let current = ifNode;
    while (true) {
        const nestedIf = language.nodeTypes.elseClause
            ? current.children?.find((c: any) => c.type === language.nodeTypes.elseClause)
                ?.children?.find((c: any) => c.type === language.nodeTypes.ifStatement)
            : current.children?.find((c: any) => c.type === language.nodeTypes.ifStatement);
        if (!nestedIf) { break; }
        branches.push(nestedIf);
        current = nestedIf;
    }
    return branches;
}

// Collects every {variable, literal value} pair a single branch's own
// condition/guard compares, without wandering into its consequence body or
// into the next branch in the chain.
//
// decision: stops recursion at nodeTypes.block/elseClause and at any other
// branch of the same chain, rather than resolving "the condition" via a
// grammar-specific field name — this project's LanguageAdapter otherwise
// avoids per-grammar field lookups (see language.ts), and a bounded scan
// keeps that consistent at the cost of also scanning a branch's own
// consequence when a grammar has no block wrapper (F#), matching the same
// whole-subtree-scan tradeoff already accepted by primitiveObsession.ts
function collectBranchDiscriminants(branchNode: any, otherBranches: Set<any>, language: LanguageAdapter): Array<{ variable: string; value: string }> {
    const { nodeTypes } = language;
    const found: Array<{ variable: string; value: string }> = [];

    function isVariableRef(node: any): boolean {
        return language.variableReferenceNodeTypes.includes(node.type);
    }

    function isLiteral(node: any): boolean {
        return node.type === nodeTypes.stringLiteral || node.type === nodeTypes.integerLiteral || node.type === nodeTypes.floatLiteral;
    }

    function literalValue(node: any): string {
        return node.type === nodeTypes.stringLiteral ? stripQuotes(node.text) : node.text;
    }

    function walk(node: any) {
        if (otherBranches.has(node) || node.type === nodeTypes.block || node.type === nodeTypes.elseClause) {
            return;
        }

        for (const { left, right } of language.getEqualityComparisons(node)) {
            if (isVariableRef(left) && isLiteral(right)) {
                found.push({ variable: left.text, value: literalValue(right) });
            } else if (isVariableRef(right) && isLiteral(left)) {
                found.push({ variable: right.text, value: literalValue(left) });
            }
        }
        for (const { left, values } of language.getMembershipComparisons(node)) {
            if (isVariableRef(left)) {
                values.forEach(value => found.push({ variable: left.text, value }));
            }
        }

        for (const child of node.children || []) {
            walk(child);
        }
    }

    walk(branchNode);
    return found;
}

// The "Match Opportunity" detector: an if/elif/elif chain (or TS's nested
// `else if`) with enough branches, all discriminating on equality/membership
// checks against the same single variable, is a natural fit for Python's
// `match`, TypeScript's `switch`, or F#'s `match` — those give exhaustiveness
// hints and one comparison site instead of N.
//
// decision: requires every branch to carry its own literal comparison against
// the same variable — a chain with an unconditional catch-all `else` still
// qualifies (the else itself contributes no discriminant and isn't a
// "branch" for this check), but a chain mixing unrelated conditions across
// branches does not, since match/switch can't express that dispatch anyway
export function analyzeMatchOpportunities(
    tree: any,
    positions: PositionLookup,
    language: LanguageAdapter,
    thresholds: MatchOpportunityThresholds = DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const { nodeTypes } = language;
    const consumed = new Set<any>();

    function traverse(node: any) {
        if (node.type === nodeTypes.ifStatement && !consumed.has(node)) {
            analyzeChain(node);
        }

        for (const child of node.children || []) {
            traverse(child);
        }
    }

    function analyzeChain(topIfNode: any) {
        const branches = collectChainBranches(topIfNode, language);
        branches.slice(1).forEach(branch => consumed.add(branch));

        if (branches.length < thresholds.minBranches) {
            return;
        }

        const perBranchDiscriminants = branches.map(branch =>
            collectBranchDiscriminants(branch, new Set(branches.filter(b => b !== branch)), language)
        );

        if (perBranchDiscriminants.some(discriminants => discriminants.length === 0)) {
            return;
        }

        const candidateVariables = perBranchDiscriminants[0].map(d => d.variable);
        const commonVariable = candidateVariables.find(variable =>
            perBranchDiscriminants.every(discriminants => discriminants.some(d => d.variable === variable))
        );
        if (!commonVariable) {
            return;
        }

        const position = positions.toPosition(topIfNode.startIndex);
        violations.push({
            line: position.line,
            column: position.column,
            type: VIOLATION_TYPE.MATCH_OPPORTUNITY,
            severity: SEVERITY.LOW,
            message: `This ${branches.length}-way if/elif chain all branch on '${commonVariable}'. Consider a match/switch statement for clearer, exhaustiveness-checked dispatch.`
        });
    }

    traverse(tree.rootNode);
    return violations;
}
