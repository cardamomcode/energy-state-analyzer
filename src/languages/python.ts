import { LanguageAdapter } from '../core/language';

// decision: shared by extractTypedParameter/extractReturnType below - both check a node's
// type against Python's grammar node-type name for a type annotation ('type', wrapping
// either a plain identifier or a generic_type); a literal repeated across both would trip
// the magic-string detector's own duplicate-string check.
const TYPE_ANNOTATION_NODE_TYPE = 'type';

// decision: shared by isPositionalCallArgument and getBaseClassNames below - both check a
// node's type against Python's grammar node-type name for a call's parenthesized argument list
// (used for a function call in the former, a class's base-class list in the latter, since
// Python's grammar reuses the same node shape for both); a literal repeated across both would
// trip the magic-string detector's own duplicate-string check.
const ARGUMENT_LIST_NODE_TYPE = 'argument_list';

// decision: shared by isFormattedOrInterpolatedString and getBaseClassNames below - both check
// a node's type against Python's grammar node-type name for a dotted attribute access
// (`a.b.C`); a literal repeated across both would trip the magic-string detector's own
// duplicate-string check.
const ATTRIBUTE_NODE_TYPE = 'attribute';

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
        'if_statement',
        'elif_clause',
        'while_statement',
        'for_statement',
        'except_clause',
        'conditional_expression',
        'match_statement'
    ],
    cognitiveNestedDecisionTypes: [
        'if_statement',
        'elif_clause',
        'for_statement',
        'while_statement',
        'except_clause',
        'match_statement'
    ],
    nestingControlTypes: [
        'if_statement',
        'for_statement',
        'while_statement',
        'with_statement',
        'try_statement',
        'match_statement'
    ],
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
    },
    variableReferenceNodeTypes: ['identifier', 'attribute'],
    extractTypedParameter(node: any): { name: string; type: string } | null {
        if (node?.type !== 'typed_parameter' && node?.type !== 'typed_default_parameter') {
            return null;
        }
        const nameNode = node.children.find((c: any) => c.type === 'identifier');
        const typeNode = node.children.find((c: any) => c.type === TYPE_ANNOTATION_NODE_TYPE);
        if (!nameNode || !typeNode) {
            return null;
        }
        return { name: nameNode.text, type: typeNode.text };
    },
    extractReturnType(node: any): string | null {
        const arrowIndex = node?.children?.findIndex((c: any) => c.type === '->') ?? -1;
        if (arrowIndex === -1) {
            return null;
        }
        const typeNode = node.children[arrowIndex + 1];
        return typeNode?.type === TYPE_ANNOTATION_NODE_TYPE ? typeNode.text : null;
    },
    genericBrackets: { open: '[', close: ']' },
    primitiveTypeNames: new Set(['str', 'int', 'float', 'bool', 'bytes']),
    keywordOnlyBoundaryTypes: ['keyword_separator', 'list_splat_pattern'],
    distinctTypeAdvice: 'NewType or a dataclass',
    getEqualityComparisons(node: any): Array<{ left: any; right: any }> {
        if (node?.type !== 'comparison_operator') {
            return [];
        }
        const results: Array<{ left: any; right: any }> = [];
        const children = node.children;
        for (let i = 1; i < children.length - 1; i++) {
            if (children[i].type === '==') {
                results.push({ left: children[i - 1], right: children[i + 1] });
            }
        }
        return results;
    },
    // decision: only Python gets this — TS's equivalent is a `.includes()` call expression
    // (not a comparison node) and F# has no direct construct; both still accumulate distinct
    // literals across separate equality comparisons via getEqualityComparisons.
    getMembershipComparisons(node: any): Array<{ left: any; values: string[] }> {
        if (node?.type !== 'comparison_operator') {
            return [];
        }
        const results: Array<{ left: any; values: string[] }> = [];
        const children = node.children;
        for (let i = 1; i < children.length - 1; i++) {
            if (children[i].type !== 'in') {
                continue;
            }
            const left = children[i - 1];
            const right = children[i + 1];
            if (right.type !== 'tuple' && right.type !== 'list' && right.type !== 'set') {
                continue;
            }
            const values: string[] = [];
            let allStrings = true;
            for (const child of right.children) {
                if (!child.isNamed) {
                    continue;
                }
                if (child.type !== 'string') {
                    allStrings = false;
                    break;
                }
                values.push(child.text.slice(1, -1));
            }
            if (allStrings && values.length > 0) {
                results.push({ left, values });
            }
        }
        return results;
    },
    getElseIfBranches(node: any): any[] {
        return node?.children?.filter((c: any) => c.type === 'elif_clause') ?? [];
    },
    subscriptNodeTypes: ['subscript'],
    isFormattedOrInterpolatedString(node: any): boolean {
        if (node?.children?.some((c: any) => c.type === 'interpolation')) {
            // f-string
            return true;
        }
        // decision: compares node identity by `.id`, not `===` — web-tree-sitter mints a fresh
        // JS wrapper object on every `.children`/`.parent` access, so two accessors that reach
        // the same underlying tree node are not reference-equal even though `.id` matches
        const parent = node?.parent;
        if (
            parent?.type === 'binary_operator' &&
            parent.children?.[0]?.id === node.id &&
            parent.children?.some((c: any) => c.type === '%')
        ) {
            // "%s" % value
            return true;
        }
        if (parent?.type === ATTRIBUTE_NODE_TYPE && parent.children?.[0]?.id === node.id) {
            const methodName = parent.children?.find((c: any) => c.type === 'identifier');
            if (methodName?.text === 'format' && parent.parent?.type === 'call') {
                return true;
            }
        }
        return false;
    },
    isDefaultParameterValue(node: any): boolean {
        const parent = node?.parent;
        return (
            (parent?.type === 'default_parameter' || parent?.type === 'typed_default_parameter') &&
            parent.children?.[parent.children.length - 1]?.id === node.id
        );
    },
    isBooleanLiteral(node: any): boolean {
        return node?.type === 'true' || node?.type === 'false';
    },
    // A keyword argument (`retries=True`) wraps the literal in its own `keyword_argument`
    // node, so a labeled boolean's parent is never `argument_list` directly.
    isPositionalCallArgument(node: any): boolean {
        return node?.parent?.type === ARGUMENT_LIST_NODE_TYPE && node.parent.parent?.type === 'call';
    },
    // Python has no dedicated compile-time-constant marker — module-scope assignment is the
    // only signal (see isInConstantContext in magicNumber.ts).
    isExplicitConstant(): boolean {
        return false;
    },
    // `import os` -> source 'os'; `from foo.bar import a, b, c` -> source 'foo.bar' (the names
    // after `import` are irrelevant, they're all the same dependency). `import os, sys` (two
    // unrelated modules on one line) is rare enough that only the first is used as the source -
    // undercounting a line like that is the safe direction, since it only reduces false positives.
    importSource(node: any): string {
        const children = node?.children ?? [];
        if (node?.type === 'import_from_statement') {
            const importIdx = children.findIndex((c: any) => c.type === 'import');
            return children[importIdx - 1]?.text ?? node.text ?? '';
        }
        const dotted = children.find((c: any) => c.type === 'dotted_name');
        return dotted?.text ?? node?.text ?? '';
    },
    classDefinitionNodeTypes: ['class_definition'],
    getClassName(node: any): string | null {
        return node?.children?.find((c: any) => c.type === 'identifier')?.text ?? null;
    },
    // `class Foo(Bar, Baz):` -> ['Bar', 'Baz']; `class Foo(meta=Meta):` skips the keyword_argument
    // (not a base class); `class Foo(pkg.Bar):` -> ['pkg.Bar'] via the attribute node's own text.
    getBaseClassNames(node: any): string[] {
        const argumentList = node?.children?.find((c: any) => c.type === ARGUMENT_LIST_NODE_TYPE);
        if (!argumentList) {
            return [];
        }
        return argumentList.children
            .filter((c: any) => c.type === 'identifier' || c.type === ATTRIBUTE_NODE_TYPE)
            .map((c: any) => c.text);
    }
};
