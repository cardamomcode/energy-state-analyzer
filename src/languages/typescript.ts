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
        expressionStatement: null, // TS/JS has no string-literal docstring convention
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
    }
};
