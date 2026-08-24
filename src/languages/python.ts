import { LanguageAdapter } from '../core/language';

export const PYTHON: LanguageAdapter = {
    id: 'python',
    grammarPath: 'grammars/tree-sitter-python.wasm',
    nodeTypes: {
        block: 'block',
        parameters: 'parameters',
        ifStatement: 'if_statement',
        elseClause: 'else_clause',
        forStatement: 'for_statement',
        whileStatement: 'while_statement',
        conditionalExpression: 'conditional_expression',
        lambda: 'lambda',
        importStatement: 'import_statement',
        importFromStatement: 'import_from_statement',
        expressionStatement: 'expression_statement',
        assignment: 'assignment',
        module: 'module',
        exportStatement: null,
        comment: 'comment',
        integerLiteral: 'integer',
        floatLiteral: 'float',
        stringLiteral: 'string'
    },
    isFunctionDefinition(node: any): boolean {
        return node?.type === 'function_definition';
    },
    parameterChildTypes: ['identifier', 'default_parameter'],
    decisionNodeTypes: [
        'if_statement', 'elif_clause', 'while_statement', 'for_statement',
        'except_clause', 'conditional_expression', 'match_statement'
    ],
    cognitiveNestedDecisionTypes: [
        'if_statement', 'elif_clause', 'for_statement', 'while_statement', 'except_clause', 'match_statement'
    ],
    nestingControlTypes: ['if_statement', 'for_statement', 'while_statement', 'with_statement', 'match_statement'],
    getBooleanOperator(node: any): 'and' | 'or' | null {
        if (!node || node.type !== 'boolean_operator') {
            return null;
        }
        const opToken = node.children?.find((c: any) => c.type === 'and' || c.type === 'or');
        return opToken ? (opToken.type as 'and' | 'or') : null;
    },
    entersNestedScope(node: any): boolean {
        return node?.type === 'block';
    },
    // decision: `else_clause` is shared by if/for/while/try in tree-sitter-python, but only a
    // try's else is a real decision point (mirrors ruff's C901: a non-vacuous try-else adds 1).
    // if/for/while's else already scores 0 via decisionNodeTypes, matching how plain `else`
    // (as opposed to `elif`) is excluded there — this predicate exists to avoid conflating
    // try's else with those.
    isTryElseClause(node: any): boolean {
        return node?.type === 'else_clause' && node?.parent?.type === 'try_statement';
    }
};
