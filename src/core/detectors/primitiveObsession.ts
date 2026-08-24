import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// decision: unqualified primitive types only — `str`/`int`/`float`/`bool`/`bytes` carry no
// semantic distinction between a parameter's meaning and its representation, unlike
// `list[str]` or a project-defined alias, which already narrow intent somewhat
const PRIMITIVE_TYPES = new Set(['str', 'int', 'float', 'bool', 'bytes']);

interface TypedParam {
    name: string;
    type: string;
    node: any;
}

function extractTypedParam(node: any): TypedParam | null {
    if (node.type !== 'typed_parameter' && node.type !== 'typed_default_parameter') {
        return null;
    }
    const nameNode = node.children.find((c: any) => c.type === 'identifier');
    const typeNode = node.children.find((c: any) => c.type === 'type');
    if (!nameNode || !typeNode) {
        return null;
    }
    return { name: nameNode.text, type: typeNode.text, node };
}

// Sub-check A ("parameter swap risk"): two adjacent parameters sharing the
// same unqualified primitive type are indistinguishable at the call site —
// nothing stops a caller passing them in the wrong order.
function findParameterCollisions(paramsNode: any, positions: PositionLookup): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const typed = paramsNode.children
        .map(extractTypedParam)
        .filter((p: TypedParam | null): p is TypedParam => p !== null);

    for (let i = 0; i < typed.length - 1; i++) {
        const a = typed[i];
        const b = typed[i + 1];
        if (a.type === b.type && PRIMITIVE_TYPES.has(a.type)) {
            const position = positions.toPosition(a.node.startIndex);
            violations.push({
                line: position.line,
                column: position.column,
                type: VIOLATION_TYPE.PRIMITIVE_OBSESSION,
                severity: SEVERITY.MEDIUM,
                message: `Primitive obsession: consecutive parameters '${a.name}: ${a.type}' and '${b.name}: ${b.type}' share the same primitive type — a caller can swap them and nothing will complain. Consider distinct types (NewType, dataclass) so the type checker catches it.`
            });
        }
    }
    return violations;
}

function stripQuotes(text: string): string {
    return text.slice(1, -1);
}

function isVariableRef(node: any): boolean {
    return node.type === 'identifier' || node.type === 'attribute';
}

// Returns the string literal values held in a collection literal, or null if
// the node isn't one, or contains anything other than string literals.
function collectStringSetValues(node: any, stringLiteralType: string): string[] | null {
    if (node.type !== 'tuple' && node.type !== 'list' && node.type !== 'set') {
        return null;
    }
    const values: string[] = [];
    for (const child of node.children) {
        if (!child.isNamed) {
            continue;
        }
        if (child.type !== stringLiteralType) {
            return null;
        }
        values.push(stripQuotes(child.text));
    }
    return values;
}

// Sub-check B ("stringly-typed control flow"): a variable repeatedly
// compared against distinct string literals is a de facto enum encoded as
// strings — no exhaustiveness checking, no typo protection at the type level.
//
// assumption: scoped to one function body at a time — the same variable name reused
// across unrelated functions (e.g. two functions both naming a parameter `status`) does
// not accumulate into a single false positive
function findStringlyTypedControlFlow(functionNode: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const { nodeTypes } = language;
    const valuesByVariable = new Map<string, Set<string>>();
    const firstOccurrence = new Map<string, any>();

    function record(varNode: any, values: string[]) {
        const key = varNode.text;
        if (!valuesByVariable.has(key)) {
            valuesByVariable.set(key, new Set());
            firstOccurrence.set(key, varNode);
        }
        const set = valuesByVariable.get(key)!;
        values.forEach(v => set.add(v));
    }

    function traverse(node: any) {
        if (node.type === 'comparison_operator') {
            const children = node.children;
            for (let i = 1; i < children.length - 1; i++) {
                const opToken = children[i];
                const left = children[i - 1];
                const right = children[i + 1];

                if (opToken.type === '==') {
                    if (isVariableRef(left) && right.type === nodeTypes.stringLiteral) {
                        record(left, [stripQuotes(right.text)]);
                    } else if (isVariableRef(right) && left.type === nodeTypes.stringLiteral) {
                        record(right, [stripQuotes(left.text)]);
                    }
                } else if (opToken.type === 'in' && isVariableRef(left)) {
                    const values = collectStringSetValues(right, nodeTypes.stringLiteral!);
                    if (values && values.length > 0) {
                        record(left, values);
                    }
                }
            }
        }
        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(functionNode);

    const violations: EnergyViolation[] = [];
    for (const [key, values] of valuesByVariable) {
        if (values.size >= 3) {
            const node = firstOccurrence.get(key);
            const position = positions.toPosition(node.startIndex);
            const sample = Array.from(values).slice(0, 4);
            violations.push({
                line: position.line,
                column: position.column,
                type: VIOLATION_TYPE.PRIMITIVE_OBSESSION,
                severity: SEVERITY.LOW,
                message: `Stringly-typed control flow: '${key}' is compared against ${values.size} distinct string literals (${sample.join(', ')}${values.size > sample.length ? ', …' : ''}). Consider an Enum or Literal type to catch typos and get exhaustiveness checking.`
            });
        }
    }
    return violations;
}

// The "Primitive Obsession" detector: strings and numbers standing in for
// what should be a distinct, validated type. Python-only for now — both
// sub-checks lean on grammar-specific node types (typed_parameter,
// comparison_operator) that other adapters don't expose yet.
export function analyzePrimitiveObsession(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    if (language.id !== 'python') {
        return [];
    }

    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (language.isFunctionDefinition(node)) {
            const paramsNode = node.children.find((c: any) => c.type === language.nodeTypes.parameters);
            if (paramsNode) {
                violations.push(...findParameterCollisions(paramsNode, positions));
            }
            violations.push(...findStringlyTypedControlFlow(node, positions, language));
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return violations;
}
