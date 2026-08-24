import { EnergyViolation } from '../types';
import { createPositionLookup } from './position';
import { LanguageAdapter } from './language';
import { analyzeNesting, NestingThresholds, DEFAULT_NESTING_THRESHOLDS } from './detectors/nesting';
import { analyzeFunctionComplexity, CyclomaticThresholds, DEFAULT_CYCLOMATIC_THRESHOLDS } from './detectors/cyclomatic';
import { analyzeCognitiveComplexity, CognitiveThresholds, DEFAULT_COGNITIVE_THRESHOLDS } from './detectors/cognitive';
import { analyzeFileCoherence, CoherenceThresholds, DEFAULT_COHERENCE_THRESHOLDS } from './detectors/coherence';
import { analyzeMagicValues } from './detectors/magicValues';
import { analyzeParameterCount } from './detectors/parameterCount';
import { analyzeInversionOpportunities } from './detectors/inversion';
import { analyzePrimitiveObsession } from './detectors/primitiveObsession';

export interface AnalyzeThresholds {
    nesting?: NestingThresholds;
    cyclomatic?: CyclomaticThresholds;
    cognitive?: CognitiveThresholds;
    coherence?: CoherenceThresholds;
}

// Language-agnostic entry point: runs every detector over an already-parsed
// tree-sitter tree.
//
// decision: runs all detectors unconditionally rather than letting callers opt into a subset — keeps the extension and the CLI (cli.ts) guaranteed to see the same violation set
// invariant: this module and everything it calls must not import vscode — used by both the VS Code extension (per keystroke) and the headless CLI (per file)
export function analyzeSource(
    sourceText: string,
    tree: any,
    language: LanguageAdapter,
    fileName: string,
    thresholds: AnalyzeThresholds = {}
): EnergyViolation[] {
    const positions = createPositionLookup(sourceText);
    const violations: EnergyViolation[] = [];

    violations.push(...analyzeNesting(tree, positions, language, thresholds.nesting ?? DEFAULT_NESTING_THRESHOLDS));
    violations.push(...analyzeFunctionComplexity(tree, positions, language, thresholds.cyclomatic ?? DEFAULT_CYCLOMATIC_THRESHOLDS));
    violations.push(...analyzeCognitiveComplexity(tree, positions, language, thresholds.cognitive ?? DEFAULT_COGNITIVE_THRESHOLDS));
    violations.push(...analyzeFileCoherence(tree, fileName, language, thresholds.coherence ?? DEFAULT_COHERENCE_THRESHOLDS));
    violations.push(...analyzeMagicValues(tree, positions, language));
    violations.push(...analyzeParameterCount(tree, positions, language));
    violations.push(...analyzeInversionOpportunities(tree, positions, language));
    violations.push(...analyzePrimitiveObsession(tree, positions, language));

    return violations;
}
