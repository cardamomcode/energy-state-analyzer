// Everything detectors need to know about a specific tree-sitter grammar.
// Adding support for a new language means writing one of these, not touching
// the detectors themselves.
//
// Fields are `string | null` where a grammar has no real equivalent (e.g. F#
// has no block-boundary node, TypeScript's ternary isn't reused for plain
// if/else) — a `null` field simply never matches any real node.type, so
// detectors degrade to "this check never fires" rather than needing extra
// guards at every call site.

export interface LanguageNodeTypes {
    block: string | null;
    parameters: string;
    ifStatement: string | null;
    elseClause: string | null;
    forStatement: string | null;
    whileStatement: string | null;
    conditionalExpression: string | null;
    lambda: string | null;
    importStatement: string | null;
    importFromStatement: string | null;
    expressionStatement: string | null;
    assignment: string | null;
    module: string | null;
    // Wraps a top-level assignment when it's explicitly exported (e.g. TS/JS
    // `export const x = ...`), sitting between the assignment and the module
    // root. Null for grammars with no such wrapper (Python, F#).
    exportStatement: string | null;
    comment: string | null;
    integerLiteral: string | null;
    floatLiteral: string | null;
    stringLiteral: string | null;
}

export interface LanguageAdapter {
    id: string;
    // Relative to the extension/project root, e.g. 'grammars/tree-sitter-python.wasm'.
    grammarPath: string;
    nodeTypes: LanguageNodeTypes;
    // Whether this node represents "a function" for complexity/param-count/
    // coherence purposes. A predicate rather than a plain node-type set
    // because some grammars can't tell "function" apart from other things by
    // type alone — e.g. F#'s function_or_value_defn also covers plain
    // `let x = 5` (and monadic `let!`) bindings, distinguished only by which
    // kind of child they have.
    isFunctionDefinition(node: any): boolean;
    // Node types that count as "one parameter" among a parameters node's children.
    parameterChildTypes: string[];
    // Node types that count as a decision point for cyclomatic complexity,
    // EXCLUDING boolean and/or (those are matched via getBooleanOperator, since
    // several grammars reuse one generic binary-expression node type for every
    // infix operator instead of giving and/or their own node type).
    decisionNodeTypes: string[];
    // Node types that add "1 + current nesting depth" to cognitive complexity
    // and descend into nested scope (if/elif/for/while/except/match-like).
    cognitiveNestedDecisionTypes: string[];
    // Control-flow node types that count toward nesting-depth violations.
    nestingControlTypes: string[];
    // Returns 'and' | 'or' if this node represents that boolean operator,
    // else null. Needed because grammars differ on whether and/or get their
    // own node type (Python) or are just a binary/infix expression whose
    // operator token happens to be &&/|| (TypeScript, F#).
    getBooleanOperator(node: any): 'and' | 'or' | null;
    // Whether a child of a decision-point node counts as "inside" it for
    // nesting-depth purposes. Grammars with an explicit block/body node
    // (Python, TypeScript) only nest on that child; F# has no such wrapper,
    // so every child of a decision node is nested content.
    entersNestedScope(node: any): boolean;
}
