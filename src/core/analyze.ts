import { EnergyViolation } from '../types';
import { createPositionLookup } from './position';
import { LanguageAdapter } from './language';
import { analyzeNesting, NestingThresholds, DEFAULT_NESTING_THRESHOLDS } from './detectors/nesting';
import { analyzeFunctionComplexity, CyclomaticThresholds, DEFAULT_CYCLOMATIC_THRESHOLDS } from './detectors/cyclomatic';
import { analyzeCognitiveComplexity, CognitiveThresholds, DEFAULT_COGNITIVE_THRESHOLDS } from './detectors/cognitive';
import { analyzeFileCoherence, CoherenceThresholds, DEFAULT_COHERENCE_THRESHOLDS } from './detectors/coherence';
import { analyzeMagicValues, MagicValuesOptions, DEFAULT_MAGIC_VALUES_OPTIONS } from './detectors/magicValues';
import { analyzeParameterCount } from './detectors/parameterCount';
import { analyzeInversionOpportunities } from './detectors/inversion';
import { analyzePrimitiveObsession } from './detectors/primitiveObsession';
import { analyzeMatchOpportunities, MatchOpportunityThresholds, DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS } from './detectors/matchOpportunity';
import { analyzeLogicalControlFlow } from './detectors/logicalControlFlow';

export interface AnalyzeThresholds {
    nesting?: NestingThresholds;
    cyclomatic?: CyclomaticThresholds;
    cognitive?: CognitiveThresholds;
    coherence?: CoherenceThresholds;
    matchOpportunity?: MatchOpportunityThresholds;
    magicValues?: MagicValuesOptions;
}

// Language-agnostic entry point: runs every detector over an already-parsed
// tree-sitter tree.
//
// decision: runs every detector unconditionally except where a detector exposes its own
// enabled flag (currently only magicValues) — a caller-selected subset would let the
// extension and the CLI (cli.ts) drift on which violations exist for the same file, but a
// per-detector on/off default that both entry points inherit identically does not
// reintroduce that drift
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
    violations.push(...analyzeMagicValues(tree, positions, language, thresholds.magicValues ?? DEFAULT_MAGIC_VALUES_OPTIONS));
    violations.push(...analyzeParameterCount(tree, positions, language));
    violations.push(...analyzeInversionOpportunities(tree, positions, language));
    violations.push(...analyzePrimitiveObsession(tree, positions, language));
    violations.push(...analyzeMatchOpportunities(tree, positions, language, thresholds.matchOpportunity ?? DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS));
    violations.push(...analyzeLogicalControlFlow(tree, positions, language));

    return violations;
}
