import { LanguageAdapter } from '../core/language';

// tree-sitter-fsharp has no block/body wrapper node — an if/for/while's
// branches are direct expression children of the construct itself (there's
// no equivalent of Python's `block` or TypeScript's `statement_block`), and
// `else` isn't a distinct node type either (an else-if chain is just a
// nested if_expression in the `else` position). Boolean operators (&&/||)
// aren't their own node type: they're an `infix_expression` whose `infix_op`
// child's text happens to be "&&" or "||", same shape as `+`/`-`/etc.
//
// `function_or_value_defn` covers both real functions (with parameters, via
// a `function_declaration_left` child) and plain `let`/`let!` bindings (via
// `value_declaration_left`) — there's no separate node type for "this let
// binding has no parameters". isFunctionDefinition below checks for the
// function_declaration_left child specifically: without that check, every
// nested `let`/`let!` inside a function body (extremely common in idiomatic
// F#, e.g. inside a MailboxProcessor `actor { let! msg = ... }`) would be
// misidentified as its own nested closure, inflating the enclosing
// function's cognitive complexity by "1 + nesting" per binding and pushing
// everything after it one nesting level too deep.
//
// The magic-value "constant context" check (assignment/module below) is
// looser here than for Python: every `let` binding, top-level or nested
// inside a function body, has the identical function_or_value_defn ->
// declaration_expression shape, so there's no cheap way to tell "named at
// module scope" apart from "named locally". Any literal that's the direct
// value of a `let` binding is treated as already-named and not flagged —
// broader than Python's module-only rule, but still aligned with the
// detector's intent (a `let NAME = ...` binding IS F#'s idiomatic way to
// name a constant, wherever it appears).
export const FSHARP: LanguageAdapter = {
    id: 'fsharp',
    grammarPath: 'grammars/tree-sitter-fsharp.wasm',
    nodeTypes: {
        block: null,
        parameters: 'argument_patterns',
        ifStatement: 'if_expression',
        elseClause: null,
        forStatement: 'for_expression',
        whileStatement: 'while_expression',
        conditionalExpression: null, // ternary-position `if` reuses if_expression, already covered
        lambda: 'fun_expression',
        importStatement: 'import_decl', // `open X`
        importFromStatement: null,
        expressionStatement: null, // F# has no string-literal docstring convention
        assignment: 'function_or_value_defn',
        module: 'declaration_expression',
        comment: 'line_comment',
        integerLiteral: 'int',
        floatLiteral: 'float',
        stringLiteral: 'string'
    },
    isFunctionDefinition(node: any): boolean {
        return node?.type === 'function_or_value_defn'
            && node.children?.some((child: any) => child.type === 'function_declaration_left');
    },
    parameterChildTypes: ['long_identifier', 'typed_pattern'],
    decisionNodeTypes: [
        'if_expression', 'elif_expression', 'for_expression', 'while_expression',
        'try_expression', 'match_expression'
    ],
    cognitiveNestedDecisionTypes: [
        'if_expression', 'elif_expression', 'for_expression', 'while_expression',
        'try_expression', 'match_expression'
    ],
    nestingControlTypes: ['if_expression', 'elif_expression', 'for_expression', 'while_expression', 'match_expression'],
    getBooleanOperator(node: any): 'and' | 'or' | null {
        if (!node || node.type !== 'infix_expression') {
            return null;
        }
        const opToken = node.children?.find((c: any) => c.type === 'infix_op');
        if (opToken?.text === '&&') {
            return 'and';
        }
        if (opToken?.text === '||') {
            return 'or';
        }
        return null;
    },
    // No block wrapper exists, so every child of a decision point is nested content.
    entersNestedScope(): boolean {
        return true;
    }
};
