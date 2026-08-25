// Reads every `energyStateAnalyzer.*` VS Code setting into the plain option/threshold objects
// the language-agnostic core/detectors expect.
//
// decision: consolidates all per-detector config readers behind one readAnalyzeThresholds() call
// — extension.ts previously imported each detector's options type and default alongside its own
// getter, which is exactly the "this file does too much" import/function sprawl the coherence
// detector flags on itself
// invariant: this module may import vscode — unlike core/*, it's extension-host-only glue
import * as vscode from 'vscode';

import { AnalyzeThresholds } from './core/analyze';
import { NestingThresholds, DEFAULT_NESTING_THRESHOLDS } from './core/detectors/nesting';
import { CyclomaticThresholds, DEFAULT_CYCLOMATIC_THRESHOLDS } from './core/detectors/cyclomatic';
import { CognitiveThresholds, DEFAULT_COGNITIVE_THRESHOLDS } from './core/detectors/cognitive';
import { CoherenceThresholds, DEFAULT_COHERENCE_THRESHOLDS } from './core/detectors/coherence';
import { MatchOpportunityThresholds, DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS } from './core/detectors/matchOpportunity';
import { MagicNumberOptions, DEFAULT_MAGIC_NUMBER_OPTIONS } from './core/detectors/magicNumber';
import { MagicStringOptions, DEFAULT_MAGIC_STRING_OPTIONS } from './core/detectors/magicString';

function getNestingThresholds(): NestingThresholds {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.nesting');
    return {
        mediumThreshold: config.get('mediumThreshold', DEFAULT_NESTING_THRESHOLDS.mediumThreshold),
        highThreshold: config.get('highThreshold', DEFAULT_NESTING_THRESHOLDS.highThreshold)
    };
}

function getCyclomaticThresholds(): CyclomaticThresholds {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.cyclomaticComplexity');
    return {
        mediumThreshold: config.get('mediumThreshold', DEFAULT_CYCLOMATIC_THRESHOLDS.mediumThreshold),
        highThreshold: config.get('highThreshold', DEFAULT_CYCLOMATIC_THRESHOLDS.highThreshold)
    };
}

function getCognitiveThresholds(): CognitiveThresholds {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.cognitiveComplexity');
    return {
        mediumThreshold: config.get('mediumThreshold', DEFAULT_COGNITIVE_THRESHOLDS.mediumThreshold),
        highThreshold: config.get('highThreshold', DEFAULT_COGNITIVE_THRESHOLDS.highThreshold)
    };
}

function getCoherenceThresholds(): CoherenceThresholds {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.coherence');
    return {
        largeFunctionLines: config.get('largeFunctionLines', DEFAULT_COHERENCE_THRESHOLDS.largeFunctionLines),
        maxLargeFunctions: config.get('maxLargeFunctions', DEFAULT_COHERENCE_THRESHOLDS.maxLargeFunctions),
        singleDomainNameShare: config.get('singleDomainNameShare', DEFAULT_COHERENCE_THRESHOLDS.singleDomainNameShare)
    };
}

function getMatchOpportunityThresholds(): MatchOpportunityThresholds {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.matchOpportunity');
    return {
        minBranches: config.get('minBranches', DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS.minBranches)
    };
}

function getMagicNumberOptions(): MagicNumberOptions {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.magicNumber');
    return {
        enabled: config.get('enabled', DEFAULT_MAGIC_NUMBER_OPTIONS.enabled),
        allowlist: config.get('allowlist', DEFAULT_MAGIC_NUMBER_OPTIONS.allowlist)
    };
}

function getMagicStringOptions(): MagicStringOptions {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.magicString');
    return {
        enabled: config.get('enabled', DEFAULT_MAGIC_STRING_OPTIONS.enabled),
        minDuplicates: config.get('minDuplicates', DEFAULT_MAGIC_STRING_OPTIONS.minDuplicates),
        allowlist: config.get('allowlist', DEFAULT_MAGIC_STRING_OPTIONS.allowlist)
    };
}

export function readAnalyzeThresholds(): AnalyzeThresholds {
    return {
        nesting: getNestingThresholds(),
        cyclomatic: getCyclomaticThresholds(),
        cognitive: getCognitiveThresholds(),
        coherence: getCoherenceThresholds(),
        matchOpportunity: getMatchOpportunityThresholds(),
        magicNumber: getMagicNumberOptions(),
        magicString: getMagicStringOptions()
    };
}

export interface EnergyColors {
    highEnergy: string;
    mediumEnergy: string;
    lowEnergy: string;
    backgroundOpacity: number;
}

export const DEFAULT_ENERGY_COLORS: EnergyColors = {
    highEnergy: '#fb8500',
    mediumEnergy: '#ffb703',
    lowEnergy: '#99dd99',
    backgroundOpacity: 0.1
};

export function getEnergyColors(): EnergyColors {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.colors');
    return {
        highEnergy: config.get('highEnergy', DEFAULT_ENERGY_COLORS.highEnergy),
        mediumEnergy: config.get('mediumEnergy', DEFAULT_ENERGY_COLORS.mediumEnergy),
        lowEnergy: config.get('lowEnergy', DEFAULT_ENERGY_COLORS.lowEnergy),
        backgroundOpacity: config.get('backgroundOpacity', DEFAULT_ENERGY_COLORS.backgroundOpacity)
    };
}
