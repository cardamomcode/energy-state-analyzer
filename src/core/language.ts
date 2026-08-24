// Everything detectors need to know about a specific tree-sitter grammar.
// Adding support for a new language means writing one of these, not touching
// the detectors themselves.

export interface LanguageNodeTypes {
    functionDefinition: string;
    classDefinition: string;
    block: string;
    parameters: string;
    identifier: string;
    defaultParameter: string;
    ifStatement: string;
    elifClause: string;
    elseClause: string;
    forStatement: string;
    whileStatement: string;
    withStatement: string;
    exceptClause: string;
    conditionalExpression: string;
    booleanOperator: string;
    booleanAnd: string;
    booleanOr: string;
    lambda: string;
    importStatement: string;
    importFromStatement: string;
    expressionStatement: string;
    assignment: string;
    module: string;
    comment: string;
    integerLiteral: string;
    floatLiteral: string;
    stringLiteral: string;
}

export interface LanguageAdapter {
    id: string;
    // Relative to the extension/project root, e.g. 'grammars/tree-sitter-python.wasm'.
    grammarPath: string;
    nodeTypes: LanguageNodeTypes;
    // Node types that count as a decision point for cyclomatic complexity.
    decisionNodeTypes: string[];
    // Control-flow node types that count toward nesting-depth violations.
    nestingControlTypes: string[];
}
