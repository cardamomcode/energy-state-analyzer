import { LanguageAdapter } from '../core/language';

function isConstPropertyDeclaration(node: any): boolean {
    const modifiers = node.children?.find((c: any) => c.type === 'modifiers');
    return (
        modifiers?.children?.some(
            (modifier: any) =>
                modifier.type === 'property_modifier' && modifier.children?.some((c: any) => c.type === 'const')
        ) ?? false
    );
}

// decision: a leading annotation (`@VisibleForTesting const val X = 5`) makes this
// tree-sitter-kotlin grammar (v1.1.0) lose the property_declaration/modifiers shape entirely
// and instead parse the whole line as a generic `assignment` whose LHS is an
// `annotated_expression` wrapping an `infix_expression` with `const`/`val`/the name as three
// bare identifier tokens (verified by dumping the parse tree) — recognize that specific
// misparse shape so an annotated const val isn't wrongly flagged as magic.
function isAnnotatedConstValMisparse(node: any): boolean {
    const annotated = node.children?.find((c: any) => c.type === 'annotated_expression');
    const infix = annotated?.children?.find((c: any) => c.type === 'infix_expression');
    const identifiers = infix?.children?.filter((c: any) => c.type === 'identifier') ?? [];
    return identifiers.length === 3 && identifiers[0]?.text === 'const' && identifiers[1]?.text === 'val';
}

// tree-sitter-kotlin (tree-sitter-grammars/tree-sitter-kotlin, v1.1.0) has a real
// block wrapper like Python/TS, but its `else` is a bare keyword token with no
// wrapper node at all: an `else if` chain's next `if_expression` is a direct
// child of the previous one, not nested inside an else_clause (TS) nor a flat
// elif sibling (Python/F#). See the ADC in matchOpportunity.ts's
// collectChainBranches and inversion.ts's hasElse check for how that third shape
// is handled — this adapter alone (elseClause: null, getElseIfBranches: []) is
// not sufficient for those two detectors, unlike every other adapter.
export const KOTLIN: LanguageAdapter = {
    id: 'kotlin',
    grammarPath: 'grammars/tree-sitter-kotlin.wasm',
    nodeTypes: {
        block: 'block',
        parameters: 'function_value_parameters',
        ifStatement: 'if_expression',
        elseClause: null,
        forStatement: 'for_statement',
        whileStatement: 'while_statement',
        // if_expression already covers ternary-style use (Kotlin has no separate ternary node)
        conditionalExpression: null,
        lambda: 'lambda_literal',
        importStatement: 'import',
        importFromStatement: null,
        expressionStatement: null,
        // decision: 'property_declaration' (val/var NAME = value), not 'assignment' (bare
        // reassignment `x = 5`, no val/var) — the only consumer (magicNumber.ts's
        // isInConstantContext) wants "is this literal the value of a named declaration", which
        // is what Python's `assignment`/TS's `lexical_declaration` mean there too
        assignment: 'property_declaration',
        module: 'source_file',
        exportStatement: null,
        // grammar splits line_comment/block_comment; this single-string field can only
        // name one — block comments are a minor documented gap (inversion.ts's statement
        // filter is the only consumer, and only for a comment as literally the first line)
        comment: 'line_comment',
        integerLiteral: 'number_literal',
        floatLiteral: 'float_literal',
        stringLiteral: 'string_literal'
    },
    isFunctionDefinition(node: any): boolean {
        return node?.type === 'function_declaration';
    },
    parameterChildTypes: ['parameter'],
    decisionNodeTypes: ['if_expression', 'for_statement', 'while_statement', 'when_expression', 'catch_block'],
    cognitiveNestedDecisionTypes: [
        'if_expression',
        'for_statement',
        'while_statement',
        'when_expression',
        'catch_block'
    ],
    nestingControlTypes: ['if_expression', 'for_statement', 'while_statement', 'try_expression'],
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
        return node?.type === 'block';
    },
    // Kotlin's try/catch has no else-branch construct.
    isTryElseClause(): boolean {
        return false;
    },
    variableReferenceNodeTypes: ['identifier', 'navigation_expression'],
    extractTypedParameter(node: any): { name: string; type: string } | null {
        if (node?.type !== 'parameter') {
            return null;
        }
        const nameNode = node.children.find((c: any) => c.type === 'identifier');
        const typeNode = node.children.find(isUserType);
        if (!nameNode || !typeNode) {
            return null;
        }
        const typeIdentifier = typeNode.children?.find((c: any) => c.type === 'identifier');
        if (!typeIdentifier) {
            return null;
        }
        return { name: nameNode.text, type: typeIdentifier.text };
    },
    extractReturnType(node: any): string | null {
        // decision: scans only the function node's own direct children (`:` followed by the
        // return-type node, after function_value_parameters and before function_body) - a
        // parameter's own `:` and type live one level deeper, inside function_value_parameters,
        // so this can't accidentally pick up a parameter's type instead of the return type.
        const colonIndex = node?.children?.findIndex((c: any) => c.type === ':') ?? -1;
        if (colonIndex === -1) {
            return null;
        }
        return node.children[colonIndex + 1]?.text ?? null;
    },
    genericBrackets: { open: '<', close: '>' },
    primitiveTypeNames: new Set(['Int', 'Long', 'Short', 'Byte', 'Double', 'Float', 'Boolean', 'String', 'Char']),
    // Kotlin has no enforced-keyword-only parameter syntax (named arguments are optional
    // at the call site) — see language.ts's field doc, same reasoning as F#.
    keywordOnlyBoundaryTypes: [],
    // decision: only suggests value class, not typealias — a typealias is just a synonym (the
    // compiler still sees the underlying primitive), so it wouldn't actually catch the swap this
    // warning is about, unlike Python's NewType/TS's branded type/F#'s single-case union, which
    // this field's other adapters correctly point to
    distinctTypeAdvice: 'a value class (@JvmInline value class)',
    getEqualityComparisons(node: any): Array<{ left: any; right: any }> {
        if (node?.type !== 'binary_expression') {
            return [];
        }
        const opToken = node.children.find((c: any) => c.type === '==' || c.type === '===');
        if (!opToken) {
            return [];
        }
        const operands = node.children.filter((c: any) => c !== opToken);
        if (operands.length !== 2) {
            return [];
        }
        return [{ left: operands[0], right: operands[1] }];
    },
    // Kotlin's set-membership idiom (`x in listOf(...)`) is an in_expression whose right side
    // is normally a call_expression, not a literal collection — not modeled here, same
    // precedent as typescript.ts. Repeated equality checks still accumulate via
    // getEqualityComparisons.
    getMembershipComparisons(): Array<{ left: any; values: string[] }> {
        return [];
    },
    // No flat elif node exists — Kotlin's chain is walked via the bare-nested-if fallback in
    // matchOpportunity.ts's collectChainBranches instead.
    getElseIfBranches(): any[] {
        return [];
    },
    subscriptNodeTypes: ['index_expression'],
    isFormattedOrInterpolatedString(node: any): boolean {
        return node?.children?.some((c: any) => c.type === 'interpolation') ?? false;
    },
    isDefaultParameterValue(node: any): boolean {
        // decision: compares node identity by `.id`, not `===` — see the matching comment in
        // python.ts's isFormattedOrInterpolatedString for why
        // decision: a default value isn't nested inside the `parameter` node itself —
        // function_value_parameters is a flat seq(parameter_modifiers?, parameter, ('=' expr)?),
        // so the default value's siblings (not ancestors) are the '=' token and the parameter
        const parent = node?.parent;
        if (parent?.type !== 'function_value_parameters') {
            return false;
        }
        const siblings = parent.children ?? [];
        const index = siblings.findIndex((c: any) => c.id === node.id);
        return index >= 2 && siblings[index - 1]?.type === '=' && siblings[index - 2]?.type === 'parameter';
    },
    // decision: true/false have no dedicated literal node in this grammar — they lex as plain
    // `identifier` tokens (verified: no boolean_literal rule exists). Safe to key off text since
    // true/false are hard keywords in Kotlin, not shadowable identifiers.
    isBooleanLiteral(node: any): boolean {
        return node?.type === 'identifier' && (node.text === 'true' || node.text === 'false');
    },
    // decision: every call argument (named or positional) wraps in `value_argument`, so unlike
    // the other adapters' direct-parent check, this also has to rule out a named argument
    // (`retries = true`) by checking the literal is value_argument's *first* child — a named
    // argument's value_argument instead starts with `identifier '='` before the value.
    isPositionalCallArgument(node: any): boolean {
        const valueArgument = node?.parent;
        if (valueArgument?.type !== 'value_argument' || valueArgument.parent?.type !== 'value_arguments') {
            return false;
        }
        if (valueArgument.parent.parent?.type !== 'call_expression') {
            return false;
        }
        return valueArgument.children?.[0]?.id === node.id;
    },
    // decision: `const val` is an explicit, compiler-enforced compile-time-constant marker —
    // unlike the module-scope heuristic isInConstantContext (magicNumber.ts) otherwise relies
    // on, this is valid at ANY nesting depth (a companion object's `const val` is just as much
    // a real constant as a top-level one), so it's checked as its own signal rather than folded
    // into that scope walk.
    isExplicitConstant(node: any): boolean {
        if (node?.type === 'property_declaration') {
            return isConstPropertyDeclaration(node);
        }
        if (node?.type === 'assignment') {
            return isAnnotatedConstValMisparse(node);
        }
        return false;
    },
    // `import a.b.C` -> source 'a.b' (the package, one symbol per line here since Kotlin has no
    // brace-grouped import syntax); `import a.b.*` -> source 'a.b' as-is, the qualified_identifier
    // is already the package with no trailing symbol to strip.
    importSource(node: any): string {
        const qualified = node?.children?.find((c: any) => c.type === 'qualified_identifier');
        if (!qualified?.text) {
            return node?.text ?? '';
        }
        const hasWildcard = node.children?.some((c: any) => c.type === '*');
        if (hasWildcard) {
            return qualified.text;
        }
        const lastDot = qualified.text.lastIndexOf('.');
        return lastDot === -1 ? qualified.text : qualified.text.slice(0, lastDot);
    },
    classDefinitionNodeTypes: ['class_declaration'],
    getClassName(node: any): string | null {
        return node?.children?.find((c: any) => c.type === 'identifier')?.text ?? null;
    },
    // `class Foo : Bar(), Baz` -> ['Bar', 'Baz']. Each delegation_specifier wraps either a
    // constructor_invocation (a superclass call, `Bar()`) or a bare user_type (an interface,
    // `Baz`) - both nest their name one level deeper inside a user_type node.
    getBaseClassNames(node: any): string[] {
        const specifiers = node?.children?.find((c: any) => c.type === 'delegation_specifiers');
        if (!specifiers) {
            return [];
        }
        return (specifiers.children ?? [])
            .filter((specifier: any) => specifier.type === 'delegation_specifier')
            .map(delegationSpecifierName)
            .filter((name: string | null): name is string => name !== null);
    }
};

// decision: split out of getBaseClassNames into its own function, rather than several
// `c.type === '...'` comparisons against the same `specifier` subtree inline there - that
// shape is exactly what the primitive-obsession detector's stringly-typed-control-flow check
// flags as a switch-like branch on an ad hoc string tag.
function delegationSpecifierName(specifier: any): string | null {
    const userType =
        specifier.children?.find(isUserType) ??
        specifier.children?.find((c: any) => c.type === 'constructor_invocation')?.children?.find(isUserType);
    return userType?.children?.find((c: any) => c.type === 'identifier')?.text ?? null;
}

function isUserType(node: any): boolean {
    return node?.type === 'user_type';
}
