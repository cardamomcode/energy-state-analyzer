module Energy.Core.LanguageAdapter

open Energy.Core.TreeSitter

// Per-grammar knowledge for the detectors.
//
// decision: the current 26-hook LanguageAdapter is a class-less interface of predicate callbacks;
// the idiomatic F# shape is a record of functions — data + behavior, no interface ceremony. Each
// hook becomes one record field with a pure signature.
//
// invariant: a `null`-returning hook becomes an `option`, and a null nodeTypes field becomes
// `NodeType option`. A None/null field never matches any real node.type, so the corresponding check
// degrades to "never fires" instead of needing a guard at every detector call site.
//
// decision: nodes stay Fable dynamic (`obj`) with this typed accessor layer — mirrors the current
// TS reading `.type`/`.text`/`.children` on `any` nodes — so detectors and language modules see
// pure F# signatures (Node -> bool, etc.) with no `any` leaking into our code.

/// A boolean operator node: `and` or `or`. Grammars differ on whether these get their own node
/// type (Python) or are just a binary/infix expression whose operator token is &&/|| (TS, F#).
type BooleanOperator =
    | And
    | Or

/// A parameter's declared name + type text, when it carries an explicit annotation.
type TypedParameter = { Name: string; Type: string }

/// The bracket characters wrapping generic type arguments (`[` `]` for Python, `<` `>` for TS/Kotlin/F#).
type GenericBrackets = { Open: string; Close: string }

/// One direct equality comparison (== / === / F#'s single =) a node represents. A list rather than
/// a single pair because Python's chained `a == b == c` parses as one comparison_operator holding
/// two comparisons. Operand literals are already unwrapped to the real literal node by the caller.
type EqualityComparison = { Left: Node; Right: Node }

/// One 'variable in (literal, literal, ...)' membership check ({ left; values }) — Python's `in`
/// over a tuple/list/set literal of strings. Empty for languages with no direct equivalent (TS's
/// `.includes()` is a call expression; F# has none).
type MembershipComparison = { Left: Node; Values: string list }

type ImportKind =
    | Module
    | Members
    | Wildcard
    | ScopeOpen
    | Header

type ImportBinding =
    { ImportedName: string
      LocalName: string }

type ImportInfo =
    { Kind: ImportKind
      Source: string
      Bindings: ImportBinding list }

// decision: uses `NodeType option` fields instead of required fields for grammar gaps (e.g. F#'s
// missing block node, TypeScript's ternary not reused for if/else) — one field per current
// LanguageNodeTypes member.
type NodeTypes =
    { Block: NodeType option
      Parameters: NodeType
      IfStatement: NodeType option
      ElseClause: NodeType option
      ForStatement: NodeType option
      WhileStatement: NodeType option
      ConditionalExpression: NodeType option
      Lambda: NodeType option
      ImportStatement: NodeType option
      ImportFromStatement: NodeType option
      ExpressionStatement: NodeType option
      Assignment: NodeType option
      Module: NodeType option
      // Wraps a top-level assignment when explicitly exported (e.g. TS `export const x = ...`),
      // between the assignment and the module root. Null for grammars with no such wrapper
      // (Python, F#).
      ExportStatement: NodeType option
      Comment: NodeType option
      IntegerLiteral: NodeType option
      FloatLiteral: NodeType option
      StringLiteral: NodeType option }

// decision: a function that decides something about a node takes the raw `Node` and returns a pure
// F# value; null-returning hooks become `... option`. The record below is the full current surface.
type LanguageAdapter =
    { Id: string
      // Relative to the extension/project root, e.g. 'grammars/tree-sitter-python.wasm'.
      GrammarPath: string
      NodeTypes: NodeTypes
      // Whether this node represents "a function" for complexity/param-count/coherence purposes.
      //
      // decision: a predicate instead of a plain node-type set — some grammars can't tell "function"
      // apart from other things by type alone (e.g. F#'s function_or_value_defn also covers plain
      // `let x = 5` and monadic `let!` bindings, distinguished only by their children).
      IsFunctionDefinition: Node -> bool
      // Node types that count as "one parameter" among a parameters node's children.
      ParameterChildTypes: NodeType list
      // Node types that count as a decision point for cyclomatic complexity, EXCLUDING boolean and/or
      // (matched via GetBooleanOperator, since several grammars reuse one generic binary-expression
      // node type for every infix operator instead of giving and/or their own node type).
      DecisionNodeTypes: NodeType list
      // Number of mutually exclusive control-flow outcomes for a multi-way decision node, including
      // an implicit unmatched path where it has no fallback arm. Returns None for ordinary binary
      // decisions, which therefore have two outcomes. This makes match/switch/when use their real
      // branch count for McCabe complexity instead of contributing one unconditionally.
      CyclomaticBranchCount: Node -> int option
      // Node types that add "1 + current nesting depth" to cognitive complexity AND descend into
      // nested scope (if/elif/for/while/except/match-like).
      CognitiveNestedDecisionTypes: NodeType list
      // Control-flow node types that count toward nesting-depth violations.
      NestingControlTypes: NodeType list
      GetBooleanOperator: Node -> BooleanOperator option
      // Whether a child of a decision-point node counts as "inside" it for nesting-depth purposes.
      // Grammars with an explicit block/body node (Python, TypeScript) only nest on that child; F#
      // has no such wrapper, so every child of a decision node is nested content.
      EntersNestedScope: Node -> bool
      // Whether this node is specifically a try-statement's `else` clause, as opposed to if/for/while's
      // `else` (several grammars reuse one else-clause node type for all of them). Only a try's else is
      // a cyclomatic decision point; always false for grammars with no try-else construct.
      IsTryElseClause: Node -> bool
      // Node types that count as "a reference to a plain variable" for the primitive-obsession
      // detector's stringly-typed-control-flow check (Python: identifier/attribute, TS:
      // identifier/member_expression, F#: long_identifier_or_op — whose .text is already the bare name).
      VariableReferenceNodeTypes: NodeType list
      // Given a candidate parameter node (one of ParameterChildTypes), returns its name and declared
      // type text if it carries an explicit annotation, else None (untyped/inferred -> None). Drives
      // the primitive-obsession detector's parameter-swap-risk check.
      ExtractTypedParameter: Node -> TypedParameter option
      // Given a function-definition node, returns its declared return-type text if annotated, else None
      // (inferred/untyped — same null-and-skip convention as ExtractTypedParameter). Drives the
      // file-coherence detector's type-cohesion signal alongside ExtractTypedParameter.
      ExtractReturnType: Node -> string option
      GenericBrackets: GenericBrackets
      // Unqualified primitive type names this language's swap-risk check treats as
      // interchangeable-and-therefore-risky (Python str/int/float/bool/bytes, TS string/number/
      // boolean, F# string/int/float/bool).
      PrimitiveTypeNames: Set<string>
      // Node types that begin an error-handling region — the try construct whose whole subtree (the
      // guarded body plus any catch/except/finally arms) counts as "error handling" for the
      // error-shadowing detector. Marking the entire construct, not just the handler arms, captures
      // both shadowing modes: logic buried under one catch, and a function dominated by handlers.
      // Empty for a language whose grammar exposes no try/catch construct.
      ErrorHandlingAnchorTypes: NodeType list
      // Node types that mark "every parameter after this one is keyword-only" (Python's bare `*`
      // keyword_separator and `*args` list_splat_pattern — both make positional calls to later params
      // impossible). Drives the primitive-obsession detector's parameter-swap-risk suppression. Empty
      // for languages with no such enforcement mechanism.
      KeywordOnlyBoundaryTypes: NodeType list
      // What to suggest in place of a naked primitive to fix a swap-risk violation, phrased in this
      // language's own idiom (Python: NewType/dataclass, TS: branded/nominal type, F#: single-case union).
      DistinctTypeAdvice: string
      GetEqualityComparisons: Node -> EqualityComparison list
      // Given a node, returns every 'variable in (literal, ...)' membership check it directly represents
      // as { left; values } pairs. Empty for languages with no direct equivalent (TS's `.includes()` is
      // a call expression; F# has none) — those still accumulate distinct literals via GetEqualityComparisons.
      GetMembershipComparisons: Node -> MembershipComparison list
      // Whether this literal can be used as a case/pattern value by the language's match-like
      // construct. This is narrower than the detector-wide literal types: C++ equality checks can
      // compare strings and floats even though neither is valid in a switch case.
      IsMatchCaseLiteral: Node -> bool
      // Given an if-statement/if-expression node, returns the elif-like nodes chained directly onto it
      // (Python's elif_clause, F#'s elif_expression, both flat siblings of the top if-node) — empty for
      // grammars with no flat elif node (TypeScript, where `else if` parses as an if_statement nested in
      // else_clause, and the match-opportunity detector walks that nesting itself).
      GetElseIfBranches: Node -> Node list
      // Node types where a literal sits in a collection index/key position, e.g. `d["key"]`/`arr[0]`
      // (subscript/subscript_expression). Drives the magic-string detector's "dict/object key lookup"
      // decision point and the magic-number detector's index exemption. Empty for grammars with no direct
      // subscript node (F#, which indexes via `.[i]`).
      SubscriptNodeTypes: NodeType list
      // Whether this string literal node itself carries interpolation (an f-string's `interpolation`
      // child in Python) or is otherwise involved in %-style / .format() formatting — either is evidence
      // the string isn't a bare magic value. Always false for grammars where an interpolated string never
      // shares StringLiteral's node type (TS template literals parse as `template_string`).
      IsFormattedOrInterpolatedString: Node -> bool
      // Whether this node is the default-value operand of an optional parameter (Python's
      // default_parameter/typed_default_parameter, TS's optional_parameter) — exempts it from
      // magic-number flagging.
      IsDefaultParameterValue: Node -> bool
      // Whether this node is a boolean literal (Python/TS true/false, F#'s const node wrapping a bool
      // child). Drives the opaque-boolean-literal detector; deliberately narrower than the rule's own
      // stated scope (which also floats flag-like bare 0/1 as a future option).
      IsBooleanLiteral: Node -> bool
      // Given a node for which IsBooleanLiteral is true, whether it sits as a direct, unlabeled positional
      // argument in a call — i.e. not a keyword argument (Python `retries=True`), not an object-literal
      // field (TS `{ retries: true }`), not F#'s named-argument syntax (`retries = true`), and not simply
      // unrelated to any call (`let ok = true`). Each language encodes "labeled" differently, so the check
      // lives here rather than in the detector.
      IsPositionalCallArgument: Node -> bool
      // Whether this ancestor node is explicitly marked by the language as a compile-time constant (Kotlin's
      // `const val`), as opposed to merely sitting at module scope. Stronger, scope-independent signal —
      // exempts magic-number flagging even nested inside a class/companion object. Always false for languages
      // with no such marker (Python, TS, F#), which rely on the module-scope heuristic alone. Called on every
      // ancestor while walking up from the literal (not just ones matching nodeTypes.Assignment).
      IsExplicitConstant: Node -> bool
      // Given an import node, preserves both its dependency source and the names it introduces locally.
      // Coherence uses these separately: source count measures dependency breadth; bindings/open forms
      // measure local vocabulary and scope pollution.
      ImportInfo: Node -> ImportInfo list
      // Whether a node introduces a class-like scope for the file-coherence detector's
      // class-relatedness check — methods nested inside one are grouped by their enclosing class
      // instead of counted as free-standing functions. A predicate is required because C++ uses the
      // same class_specifier/struct_specifier node types for definitions and forward declarations.
      IsClassDefinition: Node -> bool
      // Whether a class method has no instance receiver. God-class scoring intentionally excludes
      // all-static classes because they are namespaces of functions, not stateful object responsibilities.
      IsStaticMethod: Node -> bool
      // Given a class-definition node, returns its declared name, or None if it can't be determined. Always
      // called with a node for which IsClassDefinition returns true.
      GetClassName: Node -> string option
      // Given a class-definition node, returns the names of every class it directly extends/implements, as
      // written in source (not resolved against imports). Used two ways by checkClassRelatedness: linked
      // directly if one's base is the other's name; linked as siblings if they share a base name in common.
      GetBaseClassNames: Node -> string list }
