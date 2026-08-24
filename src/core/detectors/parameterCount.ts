import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// The "Parameter Explosion" detector
export function analyzeParameterCount(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const { nodeTypes } = language;

    function traverse(node: any) {
        if (node.type === nodeTypes.functionDefinition) {
            const params = node.children.find((child: any) => child.type === nodeTypes.parameters);
            if (params) {
                const paramCount = params.children.filter((child: any) =>
                    child.type === nodeTypes.identifier || child.type === nodeTypes.defaultParameter
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
