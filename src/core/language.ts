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
    // Given a function-definition node (per isFunctionDefinition), returns its declared
    // return-type text if one is explicitly annotated, else null (inferred/untyped return
    // types return null — same null-and-skip convention as extractTypedParameter). Drives
    // the file-coherence detector's type-cohesion signal alongside extractTypedParameter.
    extractReturnType(node: any): string | null;
    // The bracket characters this language's grammar uses to wrap generic type arguments
    // (Python: `[` `]`, TS/Kotlin/F#: `<` `>`), so callers can strip a raw type-text blob
    // like "Iterable[T]"/"Iterable<T>" down to its base type name "Iterable" without each
    // detector special-casing per-language generic syntax.
    genericBrackets: { open: string; close: string };
    // Unqualified primitive type names this language's swap-risk check
    // treats as interchangeable-and-therefore-risky (Python's str/int/
    // float/bool/bytes, TS's string/number/boolean, F#'s string/int/float/
    // bool).
    primitiveTypeNames: Set<string>;
    // Node types that mark "every parameter after this one is keyword-only" (Python's
    // bare `*` keyword_separator and `*args` list_splat_pattern — both make positional
    // calls to later parameters impossible). Drives the primitive-obsession detector's
    // parameter-swap-risk suppression: two same-typed params can't be flagged for swap
    // risk if a caller is structurally unable to pass them positionally in the first
    // place. Empty for languages with no such enforcement mechanism (TypeScript's
    // destructured-object pattern isn't modeled here since extractTypedParameter
    // already never matches it; F#'s named-argument syntax is optional at the call
    // site, so it doesn't prevent a future positional call and isn't a valid mitigation).
    keywordOnlyBoundaryTypes: string[];
    // What to suggest in place of a naked primitive to fix a swap-risk
    // violation, phrased in this language's own idiom (Python: NewType/
    // dataclass, TypeScript: a branded/nominal type, F#: a single-case
    // union type).
    distinctTypeAdvice: string;
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
    // Node types where a literal sits in a collection index/key position, e.g.
    // Python/TS `d["key"]` or `arr[0]` (`subscript`/`subscript_expression`).
    // Drives the magic-string detector's "dict/object key lookup" decision
    // point and the magic-number detector's index exemption. Empty for
    // grammars with no direct subscript node (F#, which indexes via `.[i]`).
    subscriptNodeTypes: string[];
    // Whether this string literal node itself carries interpolation (an
    // f-string's `interpolation` child in Python) or is otherwise involved in
    // %-style / .format() string formatting — either is itself evidence the
    // string isn't a bare magic value. Always false for grammars where an
    // interpolated string never shares stringLiteral's node type (TS template
    // literals parse as `template_string`, not `string`, so they're already
    // skipped by the traversal itself).
    isFormattedOrInterpolatedString(node: any): boolean;
    // Whether this node is the default-value operand of an optional
    // parameter (Python's `default_parameter`/`typed_default_parameter`, TS's
    // `optional_parameter`) — exempts default parameter values from
    // magic-number flagging.
    isDefaultParameterValue(node: any): boolean;
    // Whether this node is a boolean literal (Python's `true`/`false`, TS's `true`/
    // `false`, F#'s `const` node wrapping a `bool` child). Drives the
    // opaque-boolean-literal detector; deliberately narrower than the rule's own
    // stated scope, which also floats flag-like bare `0`/`1` as a future option —
    // left out here to avoid false positives on ordinary numeric arguments.
    isBooleanLiteral(node: any): boolean;
    // Given a node for which isBooleanLiteral is true, whether it sits as a direct,
    // unlabeled positional argument in a call — i.e. not a keyword argument
    // (Python's `retries=True`), not a field of an object-literal argument (TS's
    // `{ retries: true }`), not F#'s named-argument syntax (`retries = true`), and
    // not simply unrelated to any call at all (e.g. `let ok = true`). Each language
    // encodes "labeled" differently, so the check lives here rather than in the
    // detector: a labeled boolean's grammar path to its call never matches this
    // predicate's direct-parent check, so no separate "is labeled" hook is needed.
    isPositionalCallArgument(node: any): boolean;
    // Whether this ancestor node is explicitly marked by the language itself as a compile-time
    // constant (Kotlin's `const val`), as opposed to merely sitting at module scope. This is a
    // stronger, scope-independent signal — it exempts magic-number flagging even nested inside
    // a class/companion object/object declaration, where the module-scope heuristic below can't
    // reach. Always false for languages with no such marker (Python, TS, F#), which still rely
    // on the module-scope heuristic alone. Called on every ancestor while walking up from the
    // literal (not just ones matching nodeTypes.assignment), since a grammar can misparse the
    // constant declaration into a different node shape entirely (see kotlin.ts).
    isExplicitConstant(node: any): boolean;
    // Given an import node (matched via nodeTypes.importStatement/importFromStatement), returns
    // the module/package it draws from, used to count *distinct dependencies* rather than raw
    // import lines for the coherence detector's import-sprawl check. This distinction matters
    // because grammars differ in how many lines one dependency costs: TS's `import { a, b, c }
    // from 'x'` and Python's `from x import a, b, c` both bundle arbitrarily many symbols from
    // one module into a single import line, but Kotlin has no such grouping syntax — each
    // symbol needs its own `import` line, and idiomatic style (ktlint's no-wildcard-imports)
    // forbids collapsing them with `import x.*`. Without this, a Kotlin file pulling 11 symbols
    // from 3 packages reads as 3x more import-sprawl than equivalent TS/Python, even though its
    // actual coupling is identical. Kotlin/F# return the package/module path (everything but the
    // trailing symbol name for Kotlin; the whole `open`-ed path for F#, which is already
    // per-module); Python/TS return the module string after from/'from '.
    importSource(node: any): string;
    // Node types that introduce a class-like scope for the file-coherence detector's
    // class-relatedness check — methods nested inside one of these are grouped by their
    // enclosing class instead of counted as free-standing functions (a class's own method
    // count isn't file-coherence's concern; see coherence.ts's checkClassRelatedness). Empty
    // for F#, which has no idiomatic class-per-file OOP pattern this check targets.
    classDefinitionNodeTypes: string[];
    // Given a class-definition node (per classDefinitionNodeTypes), returns its declared name,
    // or null if it can't be determined. Always called with a node whose type is one of
    // classDefinitionNodeTypes.
    getClassName(node: any): string | null;
    // Given a class-definition node, returns the names of every class it directly extends or
    // implements, as written in the source (not resolved against imports — a name here might
    // refer to a class defined elsewhere in the file, or to an external one like `Exception`).
    // Used two ways by checkClassRelatedness: (1) two classes in the same file are linked
    // directly if one's base name is the other's own name; (2) two classes are linked as
    // siblings if they share a base name in common, even when that base isn't itself defined
    // in the file (e.g. a whole file of exception classes that all extend `Exception` but
    // never reference each other).
    getBaseClassNames(node: any): string[];
}
