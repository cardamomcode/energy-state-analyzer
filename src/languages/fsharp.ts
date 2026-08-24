import { LanguageAdapter } from '../core/language';

// tree-sitter-fsharp has no block/body wrapper node — an if/for/while's
// branches are direct expression children of the construct itself (there's
// no equivalent of Python's `block` or TypeScript's `statement_block`), and
// `else` isn't a distinct node type either (an else-if chain is just a
// nested if_expression in the `else` position). Boolean operators (&&/||)
// aren't their own node type: they're an `infix_expression` whose `infix_op`
// child's text happens to be "&&" or "||", same shape as `+`/`-`/etc.
//
// decision: checks for a function_declaration_left child in isFunctionDefinition rather than matching function_or_value_defn alone — that node type also covers plain `let`/`let!` bindings (via value_declaration_left), and without the check every nested `let`/`let!` inside a function body (e.g. inside a MailboxProcessor `actor { let! msg = ... }`) would be misidentified as its own nested closure
// invariant: a nested let/let! binding never inflates the enclosing function's cognitive complexity or nesting depth — only bindings with a function_declaration_left child count as a function
//
// assumption: any literal that is the direct value of a `let` binding (top-level or nested) is already named and not flagged as a magic value — broader than Python's module-only rule, since function_or_value_defn -> declaration_expression looks identical at every scope, but still aligned with the detector's intent that `let NAME = ...` IS F#'s idiomatic way to name a constant
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
        exportStatement: null,
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
    },
    // F#'s try_expression has no else-branch construct.
    isTryElseClause(): boolean {
        return false;
    }
};
