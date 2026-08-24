import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { LanguageAdapter } from '../language';

export interface CoherenceThresholds {
    // Languages like F# idiomatically have many small functions per module,
    // so raw function count alone isn't a useful sprawl signal there. What
    // matters is functions large enough to carry real complexity.
    largeFunctionLines: number;
    // Number of large functions (per largeFunctionLines) a file can contain
    // before it's flagged.
    maxLargeFunctions: number;
}

export const DEFAULT_COHERENCE_THRESHOLDS: CoherenceThresholds = {
    largeFunctionLines: 20,
    maxLargeFunctions: 5
};

function lineCount(node: any): number {
    return node.endPosition.row - node.startPosition.row + 1;
}

// The "Utils/Helpers Sprawl" detector - detects files losing coherence
export function analyzeFileCoherence(
    tree: any,
    fileName: string,
    language: LanguageAdapter,
    thresholds: CoherenceThresholds = DEFAULT_COHERENCE_THRESHOLDS
): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const functions: any[] = [];
    const imports: string[] = [];
    const { nodeTypes } = language;

    function traverse(node: any) {
        if (language.isFunctionDefinition(node)) {
            functions.push(node);
        } else if (node.type === nodeTypes.importStatement || node.type === nodeTypes.importFromStatement) {
            imports.push(node.text || '');
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);

    const largeFunctions = functions.filter(fn => lineCount(fn) > thresholds.largeFunctionLines);

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

    // Flag files with too many large functions, regardless of total function
    // count - a module with 30 small functions is fine, one with 6 sprawling
    // ones isn't.
    if (largeFunctions.length > thresholds.maxLargeFunctions) {
        violations.push({
            line: 0,
            column: 0,
            type: VIOLATION_TYPE.COHERENCE,
            severity: largeFunctions.length > thresholds.maxLargeFunctions * 1.5 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
            message: `${largeFunctions.length} functions exceed ${thresholds.largeFunctionLines} lines. Large functions carry more complexity than function count alone suggests.`
        });
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
