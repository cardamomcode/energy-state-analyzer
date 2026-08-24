// Everything detectors need to know about a specific tree-sitter grammar.
//
// decision: centralizes per-grammar node-type mapping in one adapter interface — adding a language means writing one LanguageAdapter, not touching every detector
// decision: uses `string | null` fields instead of optional fields for grammar gaps (e.g. F#'s missing block node, TypeScript's ternary not reused for if/else)
// invariant: a null field never matches any real node.type, so detectors degrade to "this check never fires" instead of needing a guard at every call site

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
    // coherence purposes.
    //
    // decision: uses a predicate instead of a plain node-type set — some grammars can't tell "function" apart from other things by type alone (e.g. F#'s function_or_value_defn also covers plain `let x = 5` and monadic `let!` bindings, distinguished only by which kind of child they have)
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
    // Whether this node is specifically a try-statement's `else` clause, as
    // opposed to if/for/while's `else` (several grammars reuse one else-clause
    // node type for all of them). Only a try's else is a cyclomatic decision
    // point; always false for grammars with no try-else construct.
    isTryElseClause(node: any): boolean;
    // Node types that count as "a reference to a plain variable" for the
    // primitive-obsession detector's stringly-typed-control-flow check
    // (Python: identifier/attribute, TS: identifier/member_expression, F#:
    // long_identifier_or_op — whose .text is already the bare name, so no
    // unwrapping down to a leaf identifier is needed).
    variableReferenceNodeTypes: string[];
    // Given a candidate parameter node (one of parameterChildTypes), returns
    // its name and declared type text if it carries an explicit type
    // annotation, else null (untyped/inferred parameters return null).
    // Drives the primitive-obsession detector's parameter-swap-risk check.
    extractTypedParameter(node: any): { name: string; type: string } | null;
    // Unqualified primitive type names this language's swap-risk check
    // treats as interchangeable-and-therefore-risky (Python's str/int/
    // float/bool/bytes, TS's string/number/boolean, F#'s string/int/float/
    // bool).
    primitiveTypeNames: Set<string>;
    // Given a node, returns every direct equality comparison (==, ===, F#'s
    // single =) it represents as {left, right} operand pairs — empty if the
    // node isn't an equality comparison. A list rather than a single pair
    // because Python's chained `a == b == c` parses as one comparison_operator
    // holding two comparisons. Literal operands are already unwrapped down to
    // the language's real literal node (e.g. F#'s `const` wrapper stripped)
    // so callers can compare `.type` against nodeTypes.stringLiteral directly.
    getEqualityComparisons(node: any): Array<{ left: any; right: any }>;
    // Given a node, returns every 'variable in (literal, literal, ...)'-style
    // membership check it directly represents (Python's `in` over a tuple/
    // list/set literal) as {left, values} pairs. Empty for languages with no
    // direct equivalent (TS's `.includes()` is a call expression, not a
    // comparison node; F# has no such construct) — those languages still get
    // the cross-comparison accumulation via getEqualityComparisons alone.
    getMembershipComparisons(node: any): Array<{ left: any; values: string[] }>;
    // Given an if-statement/if-expression node, returns the elif-like nodes
    // chained directly onto it (Python's `elif_clause`, F#'s `elif_expression`,
    // both attached as flat siblings of the top if-node itself) — empty for
    // grammars with no flat elif node (TypeScript, where `else if` instead
    // parses as an `if_statement` nested one level inside `else_clause`, and
    // the match-opportunity detector walks that nesting itself using
    // nodeTypes.elseClause/ifStatement rather than this hook).
    getElseIfBranches(node: any): any[];
}
