import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { LanguageAdapter } from '../language';

// The "Utils/Helpers Sprawl" detector - detects files losing coherence
export function analyzeFileCoherence(tree: any, fileName: string, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const functions: any[] = [];
    const imports: string[] = [];
    const { nodeTypes } = language;

    function traverse(node: any) {
        if (language.functionDefinitionTypes.includes(node.type)) {
            functions.push(node);
        } else if (node.type === nodeTypes.importStatement || node.type === nodeTypes.importFromStatement) {
            imports.push(node.text || '');
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);

    // Flag files with too many unrelated functions (utils/helpers sprawl)
    if (functions.length > 8) {
        const baseName = fileName.split('/').pop() || '';
        const isUtilsFile = baseName.includes('util') || baseName.includes('helper') || baseName.includes('common');

        if (isUtilsFile || functions.length > 12) {
            violations.push({
                line: 0,
                column: 0,
                type: VIOLATION_TYPE.COHERENCE,
                severity: functions.length > 15 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
                message: `File coherence warning: ${functions.length} functions in one file. Consider splitting by domain.`
            });
        }
    }

    // Flag excessive imports (another sign of incoherence)
    if (imports.length > 10) {
        violations.push({
            line: 0,
            column: 0,
            type: VIOLATION_TYPE.COHERENCE,
            severity: imports.length > 15 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
            message: `Import sprawl: ${imports.length} imports suggest this file does too much.`
        });
    }

    return violations;
}
