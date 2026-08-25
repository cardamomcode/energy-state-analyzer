import { LanguageAdapter } from '../core/language';

// tree-sitter-typescript is structurally close to Python's grammar: real
// block/body nodes, a distinct else_clause, and and/or (&&/||) each get
// their own node type as the operator-token child of a binary_expression.
//
// decision: treats arrow functions (`(x) => x + 1`) as `lambda`, matching Python's `lambda` — they add structural nesting in cognitive complexity but aren't analyzed by parameter-count/complexity/coherence themselves (same limitation Python already has for its own lambdas); only named `function_declaration`s and class `method_definition`s count as "a function" for those detectors
//
// tradeoff: accepts a slightly higher cognitive-complexity score for `else if` chains (else_clause's flat +1 plus the nested if's `1 + nesting`) instead of unwrapping single-if else-clauses specially — TypeScript's `else if` parses as `else_clause` wrapping a nested `if_statement`, unlike Python's flat elif sibling
export const TYPESCRIPT: LanguageAdapter = {
    id: 'typescript',
    grammarPath: 'grammars/tree-sitter-typescript.wasm',
    nodeTypes: {
        block: 'statement_block',
        parameters: 'formal_parameters',
        ifStatement: 'if_statement',
        elseClause: 'else_clause',
        forStatement: 'for_statement',
        whileStatement: 'while_statement',
        conditionalExpression: 'ternary_expression',
        lambda: 'arrow_function',
        importStatement: 'import_statement',
        importFromStatement: null, // import_statement already covers every import form
        expressionStatement: 'expression_statement',
        assignment: 'lexical_declaration',
        module: 'program',
        exportStatement: 'export_statement',
        comment: 'comment',
        integerLiteral: 'number', // TS doesn't distinguish int/float, both are "number"
        floatLiteral: null,
        stringLiteral: 'string'
    },
    isFunctionDefinition(node: any): boolean {
        return node?.type === 'function_declaration' || node?.type === 'method_definition';
    },
    parameterChildTypes: ['required_parameter', 'optional_parameter'],
    decisionNodeTypes: [
        'if_statement', 'for_statement', 'for_in_statement', 'while_statement',
        'catch_clause', 'ternary_expression'
    ],
    cognitiveNestedDecisionTypes: [
        'if_statement', 'for_statement', 'for_in_statement', 'while_statement', 'catch_clause'
    ],
    nestingControlTypes: ['if_statement', 'for_statement', 'for_in_statement', 'while_statement'],
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
        return node?.type === 'statement_block';
    },
    // JS/TS try/catch has no else-branch construct.
    isTryElseClause(): boolean {
        return false;
    },
    variableReferenceNodeTypes: ['identifier', 'member_expression'],
    extractTypedParameter(node: any): { name: string; type: string } | null {
        if (node?.type !== 'required_parameter' && node?.type !== 'optional_parameter') {
            return null;
        }
        const nameNode = node.children.find((c: any) => c.type === 'identifier');
        const typeAnnotation = node.children.find((c: any) => c.type === 'type_annotation');
        if (!nameNode || !typeAnnotation) {
            return null;
        }
        // type_annotation's children are [':', <the actual type node>]
        const typeNode = typeAnnotation.children.find((c: any) => c.type !== ':');
        if (!typeNode) {
            return null;
        }
        return { name: nameNode.text, type: typeNode.text };
    },
    primitiveTypeNames: new Set(['string', 'number', 'boolean']),
    // TS has no enforced-keyword-only parameter syntax — see language.ts's field doc.
    keywordOnlyBoundaryTypes: [],
    distinctTypeAdvice: 'a branded/nominal type (e.g. a tagged type alias)',
    getEqualityComparisons(node: any): Array<{ left: any; right: any }> {
        if (node?.type !== 'binary_expression') {
            return [];
        }
        const opToken = node.children.find((c: any) => c.type === '===' || c.type === '==');
        if (!opToken) {
            return [];
        }
        const operands = node.children.filter((c: any) => c !== opToken);
        if (operands.length !== 2) {
            return [];
        }
        return [{ left: operands[0], right: operands[1] }];
    },
    // TS's set-membership idiom is `[...].includes(x)`, a call_expression rather than a
    // comparison node — not modeled here; repeated equality checks still accumulate via
    // getEqualityComparisons.
    getMembershipComparisons(): Array<{ left: any; values: string[] }> {
        return [];
    },
    // `else if` has no flat elif node in this grammar — it's a nested if_statement one
    // level inside else_clause, which the match-opportunity detector walks itself.
    getElseIfBranches(): any[] {
        return [];
    },
    subscriptNodeTypes: ['subscript_expression'],
    // TS has no node type where an interpolated/formatted string still shares
    // stringLiteral's node type — template literals parse as `template_string`,
    // which the traversal never visits as a string literal in the first place.
    isFormattedOrInterpolatedString(): boolean {
        return false;
    },
    isDefaultParameterValue(node: any): boolean {
        // decision: compares node identity by `.id`, not `===` — see the matching comment in
        // python.ts's isFormattedOrInterpolatedString for why
        // decision: a parameter with a default value (`x: number = 1`) still parses as
        // `required_parameter`, not `optional_parameter` (that node type is reserved for `x?:
        // number`) — so this checks for the `=` child rather than trusting the parameter's own
        // node type to signal "has a default"
        const parent = node?.parent;
        if (parent?.type !== 'required_parameter' && parent?.type !== 'optional_parameter') {
            return false;
        }
        return parent.children?.some((c: any) => c.type === '=')
            && parent.children?.[parent.children.length - 1]?.id === node.id;
    }
};
