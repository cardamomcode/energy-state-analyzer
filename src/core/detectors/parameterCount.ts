import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// Finds the first descendant of the given type, checking each level in full
// before descending to the next. This finds a function's own parameters
// node even when it's nested a level below the function node itself (e.g.
// F#'s argument_patterns sits inside function_declaration_left), while
// still stopping before it can reach a nested function's parameters.
function findParametersNode(node: any, parametersType: string): any {
    for (const child of node.children) {
        if (child.type === parametersType) {
            return child;
        }
    }
    for (const child of node.children) {
        const found = findParametersNode(child, parametersType);
        if (found) {
            return found;
        }
    }
    return undefined;
}

// The "Parameter Explosion" detector
export function analyzeParameterCount(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (language.functionDefinitionTypes.includes(node.type)) {
            const params = findParametersNode(node, language.nodeTypes.parameters);
            if (params) {
                const paramCount = params.children.filter((child: any) =>
                    language.parameterChildTypes.includes(child.type)
                ).length;

                if (paramCount > 5) {
                    const position = positions.toPosition(node.startIndex);
                    violations.push({
                        line: position.line,
                        column: position.column,
                        type: VIOLATION_TYPE.PARAMETERS,
                        severity: paramCount > 8 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                        message: `Parameter explosion: ${paramCount} parameters. Consider using objects or builder pattern.`
                    });
                }
            }
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return violations;
}
