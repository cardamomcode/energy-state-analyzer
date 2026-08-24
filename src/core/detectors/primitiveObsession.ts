import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';
import { findParametersNode } from './parameterCount';

interface TypedParam {
    name: string;
    type: string;
    node: any;
}

// Sub-check A ("parameter swap risk"): two adjacent parameters sharing the
// same unqualified primitive type are indistinguishable at the call site —
// nothing stops a caller passing them in the wrong order.
function findParameterCollisions(paramsNode: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const typed: TypedParam[] = paramsNode.children
        .map((n: any) => {
            const extracted = language.extractTypedParameter(n);
            return extracted ? { ...extracted, node: n } : null;
        })
        .filter((p: TypedParam | null): p is TypedParam => p !== null);

    for (let i = 0; i < typed.length - 1; i++) {
        const a = typed[i];
        const b = typed[i + 1];
        if (a.type === b.type && language.primitiveTypeNames.has(a.type)) {
            const position = positions.toPosition(a.node.startIndex);
            violations.push({
                line: position.line,
                column: position.column,
                type: VIOLATION_TYPE.PRIMITIVE_OBSESSION,
                severity: SEVERITY.MEDIUM,
                message: `Primitive obsession: consecutive parameters '${a.name}: ${a.type}' and '${b.name}: ${b.type}' share the same primitive type — a caller can swap them and nothing will complain. Consider ${language.distinctTypeAdvice} so the type checker catches it.`
            });
        }
    }
    return violations;
}

function stripQuotes(text: string): string {
    return text.slice(1, -1);
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

    function isVariableRef(node: any): boolean {
        return language.variableReferenceNodeTypes.includes(node.type);
    }

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
        for (const { left, right } of language.getEqualityComparisons(node)) {
            if (isVariableRef(left) && right.type === nodeTypes.stringLiteral) {
                record(left, [stripQuotes(right.text)]);
            } else if (isVariableRef(right) && left.type === nodeTypes.stringLiteral) {
                record(right, [stripQuotes(left.text)]);
            }
        }
        for (const { left, values } of language.getMembershipComparisons(node)) {
            if (isVariableRef(left) && values.length > 0) {
                record(left, values);
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
// what should be a distinct, validated type. Both sub-checks are driven
// entirely through LanguageAdapter hooks (extractTypedParameter,
// primitiveTypeNames, getEqualityComparisons, getMembershipComparisons,
// variableReferenceNodeTypes), so Python/TypeScript/F# all run the same
// traversal logic below.
export function analyzePrimitiveObsession(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (language.isFunctionDefinition(node)) {
            const paramsNode = findParametersNode(node, language.nodeTypes.parameters);
            if (paramsNode) {
                violations.push(...findParameterCollisions(paramsNode, positions, language));
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
