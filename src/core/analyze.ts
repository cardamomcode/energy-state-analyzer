import { EnergyViolation } from '../types';
import { createPositionLookup } from './position';
import { LanguageAdapter } from './language';
import { analyzeNesting } from './detectors/nesting';
import { analyzeFunctionComplexity, CyclomaticThresholds, DEFAULT_CYCLOMATIC_THRESHOLDS } from './detectors/cyclomatic';
import { analyzeCognitiveComplexity, CognitiveThresholds, DEFAULT_COGNITIVE_THRESHOLDS } from './detectors/cognitive';
import { analyzeFileCoherence } from './detectors/coherence';
import { analyzeMagicValues } from './detectors/magicValues';
import { analyzeParameterCount } from './detectors/parameterCount';
import { analyzeInversionOpportunities } from './detectors/inversion';

export interface AnalyzeThresholds {
    cyclomatic?: CyclomaticThresholds;
    cognitive?: CognitiveThresholds;
}

// Language-agnostic entry point: runs every detector over an already-parsed
// tree-sitter tree. Used by both the VS Code extension (per keystroke) and
// the headless CLI (per file), so it must not depend on vscode.
export function analyzeSource(
    sourceText: string,
    tree: any,
    language: LanguageAdapter,
    fileName: string,
    thresholds: AnalyzeThresholds = {}
): EnergyViolation[] {
    const positions = createPositionLookup(sourceText);
    const violations: EnergyViolation[] = [];

    violations.push(...analyzeNesting(tree, positions, language));
    violations.push(...analyzeFunctionComplexity(tree, positions, language, thresholds.cyclomatic ?? DEFAULT_CYCLOMATIC_THRESHOLDS));
    violations.push(...analyzeCognitiveComplexity(tree, positions, language, thresholds.cognitive ?? DEFAULT_COGNITIVE_THRESHOLDS));
    violations.push(...analyzeFileCoherence(tree, fileName, language));
    violations.push(...analyzeMagicValues(tree, positions, language));
    violations.push(...analyzeParameterCount(tree, positions, language));
    violations.push(...analyzeInversionOpportunities(tree, positions, language));

    return violations;
}
