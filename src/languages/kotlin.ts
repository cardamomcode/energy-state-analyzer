import { LanguageAdapter } from '../core/language';

// tree-sitter-kotlin (tree-sitter-grammars/tree-sitter-kotlin, v1.1.0) has a real
// block wrapper like Python/TS, but its `else` is a bare keyword token with no
// wrapper node at all: an `else if` chain's next `if_expression` is a direct
// child of the previous one, not nested inside an else_clause (TS) nor a flat
// elif sibling (Python/F#). See the ADC in matchOpportunity.ts's
// collectChainBranches and inversion.ts's hasElse check for how that third shape
// is handled — this adapter alone (elseClause: null, getElseIfBranches: []) is
// not sufficient for those two detectors, unlike every other adapter.
export const KOTLIN: LanguageAdapter = {
    id: 'kotlin',
    grammarPath: 'grammars/tree-sitter-kotlin.wasm',
    nodeTypes: {
        block: 'block',
        parameters: 'function_value_parameters',
        ifStatement: 'if_expression',
        elseClause: null,
        forStatement: 'for_statement',
        whileStatement: 'while_statement',
        // if_expression already covers ternary-style use (Kotlin has no separate ternary node)
        conditionalExpression: null,
        lambda: 'lambda_literal',
        importStatement: 'import',
        importFromStatement: null,
        expressionStatement: null,
        // decision: 'property_declaration' (val/var NAME = value), not 'assignment' (bare
        // reassignment `x = 5`, no val/var) — the only consumer (magicNumber.ts's
        // isInConstantContext) wants "is this literal the value of a named declaration", which
        // is what Python's `assignment`/TS's `lexical_declaration` mean there too
        assignment: 'property_declaration',
        module: 'source_file',
        exportStatement: null,
        // grammar splits line_comment/block_comment; this single-string field can only
        // name one — block comments are a minor documented gap (inversion.ts's statement
        // filter is the only consumer, and only for a comment as literally the first line)
        comment: 'line_comment',
        integerLiteral: 'number_literal',
        floatLiteral: 'float_literal',
        stringLiteral: 'string_literal'
    },
    isFunctionDefinition(node: any): boolean {
        return node?.type === 'function_declaration';
    },
    parameterChildTypes: ['parameter'],
    decisionNodeTypes: [
        'if_expression', 'for_statement', 'while_statement', 'when_expression', 'catch_block'
    ],
    cognitiveNestedDecisionTypes: [
        'if_expression', 'for_statement', 'while_statement', 'when_expression', 'catch_block'
    ],
    nestingControlTypes: ['if_expression', 'for_statement', 'while_statement'],
    getBooleanOperator(node: any): 'and' | 'or' | null {
        if (!node || node.type !== 'binary_expression') {
            return null;
        }
        const opToken = node.children?.find((c: any) => c.type === '&&' || c.type === '||');
        if (!opToken) {
            return null;
        }
        return opToken.type === '&&' ? 'and' : 'or';
    },
    entersNestedScope(node: any): boolean {
        return node?.type === 'block';
    },
    // Kotlin's try/catch has no else-branch construct.
    isTryElseClause(): boolean {
        return false;
    },
    variableReferenceNodeTypes: ['identifier', 'navigation_expression'],
    extractTypedParameter(node: any): { name: string; type: string } | null {
        if (node?.type !== 'parameter') {
            return null;
        }
        const nameNode = node.children.find((c: any) => c.type === 'identifier');
        const typeNode = node.children.find((c: any) => c.type === 'user_type');
        if (!nameNode || !typeNode) {
            return null;
        }
        const typeIdentifier = typeNode.children?.find((c: any) => c.type === 'identifier');
        if (!typeIdentifier) {
            return null;
        }
        return { name: nameNode.text, type: typeIdentifier.text };
    },
    primitiveTypeNames: new Set(['Int', 'Long', 'Short', 'Byte', 'Double', 'Float', 'Boolean', 'String', 'Char']),
    // Kotlin has no enforced-keyword-only parameter syntax (named arguments are optional
    // at the call site) — see language.ts's field doc, same reasoning as F#.
    keywordOnlyBoundaryTypes: [],
    // decision: only suggests value class, not typealias — a typealias is just a synonym (the
    // compiler still sees the underlying primitive), so it wouldn't actually catch the swap this
    // warning is about, unlike Python's NewType/TS's branded type/F#'s single-case union, which
    // this field's other adapters correctly point to
    distinctTypeAdvice: 'a value class (@JvmInline value class)',
    getEqualityComparisons(node: any): Array<{ left: any; right: any }> {
        if (node?.type !== 'binary_expression') {
            return [];
        }
        const opToken = node.children.find((c: any) => c.type === '==' || c.type === '===');
        if (!opToken) {
            return [];
        }
        const operands = node.children.filter((c: any) => c !== opToken);
        if (operands.length !== 2) {
            return [];
        }
        return [{ left: operands[0], right: operands[1] }];
    },
    // Kotlin's set-membership idiom (`x in listOf(...)`) is an in_expression whose right side
    // is normally a call_expression, not a literal collection — not modeled here, same
    // precedent as typescript.ts. Repeated equality checks still accumulate via
    // getEqualityComparisons.
    getMembershipComparisons(): Array<{ left: any; values: string[] }> {
        return [];
    },
    // No flat elif node exists — Kotlin's chain is walked via the bare-nested-if fallback in
    // matchOpportunity.ts's collectChainBranches instead.
    getElseIfBranches(): any[] {
        return [];
    },
    subscriptNodeTypes: ['index_expression'],
    isFormattedOrInterpolatedString(node: any): boolean {
        return node?.children?.some((c: any) => c.type === 'interpolation') ?? false;
    },
    isDefaultParameterValue(node: any): boolean {
        // decision: compares node identity by `.id`, not `===` — see the matching comment in
        // python.ts's isFormattedOrInterpolatedString for why
        // decision: a default value isn't nested inside the `parameter` node itself —
        // function_value_parameters is a flat seq(parameter_modifiers?, parameter, ('=' expr)?),
        // so the default value's siblings (not ancestors) are the '=' token and the parameter
        const parent = node?.parent;
        if (parent?.type !== 'function_value_parameters') {
            return false;
        }
        const siblings = parent.children ?? [];
        const index = siblings.findIndex((c: any) => c.id === node.id);
        return index >= 2
            && siblings[index - 1]?.type === '='
            && siblings[index - 2]?.type === 'parameter';
    },
    // decision: true/false have no dedicated literal node in this grammar — they lex as plain
    // `identifier` tokens (verified: no boolean_literal rule exists). Safe to key off text since
    // true/false are hard keywords in Kotlin, not shadowable identifiers.
    isBooleanLiteral(node: any): boolean {
        return node?.type === 'identifier' && (node.text === 'true' || node.text === 'false');
    },
    // decision: every call argument (named or positional) wraps in `value_argument`, so unlike
    // the other adapters' direct-parent check, this also has to rule out a named argument
    // (`retries = true`) by checking the literal is value_argument's *first* child — a named
    // argument's value_argument instead starts with `identifier '='` before the value.
    isPositionalCallArgument(node: any): boolean {
        const valueArgument = node?.parent;
        if (valueArgument?.type !== 'value_argument' || valueArgument.parent?.type !== 'value_arguments') {
            return false;
        }
        if (valueArgument.parent.parent?.type !== 'call_expression') {
            return false;
        }
        return valueArgument.children?.[0]?.id === node.id;
    },
    // decision: `const val` is an explicit, compiler-enforced compile-time-constant marker —
    // unlike the module-scope heuristic isInConstantContext (magicNumber.ts) otherwise relies
    // on, this is valid at ANY nesting depth (a companion object's `const val` is just as much
    // a real constant as a top-level one), so it's checked as its own signal rather than folded
    // into that scope walk.
    isExplicitConstant(node: any): boolean {
        if (node?.type !== 'property_declaration') {
            return false;
        }
        const modifiers = node.children?.find((c: any) => c.type === 'modifiers');
        return modifiers?.children?.some((modifier: any) =>
            modifier.type === 'property_modifier'
            && modifier.children?.some((c: any) => c.type === 'const')) ?? false;
    }
};
