import { LanguageAdapter } from '../core/language';

export const PYTHON: LanguageAdapter = {
    id: 'python',
    grammarPath: 'grammars/tree-sitter-python.wasm',
    nodeTypes: {
        functionDefinition: 'function_definition',
        classDefinition: 'class_definition',
        block: 'block',
        parameters: 'parameters',
        identifier: 'identifier',
        defaultParameter: 'default_parameter',
        ifStatement: 'if_statement',
        elifClause: 'elif_clause',
        elseClause: 'else_clause',
        forStatement: 'for_statement',
        whileStatement: 'while_statement',
        withStatement: 'with_statement',
        exceptClause: 'except_clause',
        conditionalExpression: 'conditional_expression',
        booleanOperator: 'boolean_operator',
        booleanAnd: 'and',
        booleanOr: 'or',
        lambda: 'lambda',
        importStatement: 'import_statement',
        importFromStatement: 'import_from_statement',
        expressionStatement: 'expression_statement',
        assignment: 'assignment',
        module: 'module',
        comment: 'comment',
        integerLiteral: 'integer',
        floatLiteral: 'float',
        stringLiteral: 'string'
    },
    decisionNodeTypes: [
        'if_statement', 'elif_clause', 'while_statement', 'for_statement',
        'except_clause', 'and', 'or', 'conditional_expression'
    ],
    nestingControlTypes: ['if_statement', 'for_statement', 'while_statement', 'with_statement']
};
