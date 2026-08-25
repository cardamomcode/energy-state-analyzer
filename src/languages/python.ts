import { LanguageAdapter } from '../core/language';

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
        'if_statement', 'elif_clause', 'while_statement', 'for_statement',
        'except_clause', 'conditional_expression', 'match_statement'
    ],
    cognitiveNestedDecisionTypes: [
        'if_statement', 'elif_clause', 'for_statement', 'while_statement', 'except_clause', 'match_statement'
    ],
    nestingControlTypes: ['if_statement', 'for_statement', 'while_statement', 'with_statement', 'match_statement'],
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
        const typeNode = node.children.find((c: any) => c.type === 'type');
        if (!nameNode || !typeNode) {
            return null;
        }
        return { name: nameNode.text, type: typeNode.text };
    },
    primitiveTypeNames: new Set(['str', 'int', 'float', 'bool', 'bytes']),
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
    callNodeTypes: ['call'],
    callArgumentListTypes: ['argument_list'],
    getCallCalleeText(callNode: any): string {
        const callee = callNode?.children?.find((c: any) => c.type !== 'argument_list');
        return callee?.text ?? '';
    },
    isFormattedOrInterpolatedString(node: any): boolean {
        if (node?.children?.some((c: any) => c.type === 'interpolation')) {
            // f-string
            return true;
        }
        // decision: compares node identity by `.id`, not `===` — web-tree-sitter mints a fresh
        // JS wrapper object on every `.children`/`.parent` access, so two accessors that reach
        // the same underlying tree node are not reference-equal even though `.id` matches
        const parent = node?.parent;
        if (parent?.type === 'binary_operator' && parent.children?.[0]?.id === node.id
            && parent.children?.some((c: any) => c.type === '%')) {
            // "%s" % value
            return true;
        }
        if (parent?.type === 'attribute' && parent.children?.[0]?.id === node.id) {
            const methodName = parent.children?.find((c: any) => c.type === 'identifier');
            if (methodName?.text === 'format' && parent.parent?.type === 'call') {
                return true;
            }
        }
        return false;
    },
    isDefaultParameterValue(node: any): boolean {
        const parent = node?.parent;
        return (parent?.type === 'default_parameter' || parent?.type === 'typed_default_parameter')
            && parent.children?.[parent.children.length - 1]?.id === node.id;
    }
};
