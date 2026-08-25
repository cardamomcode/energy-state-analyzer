#!/usr/bin/env node
// Headless entry point: run the same detectors the extension uses, without
// vscode, so an external process (e.g. an AI coding agent) can gate on
// complexity without opening an editor.
//
// decision: exits 1 when any medium-or-high violation is found, not just high — CI/agent gating wants a single boolean signal, and severity is already visible in the emitted JSON for anyone who wants to discriminate further
import * as fs from 'fs';
import * as path from 'path';
const { Parser, Language } = require('web-tree-sitter');

import { analyzeSource, AnalyzeThresholds } from './core/analyze';
import { LanguageAdapter } from './core/language';
import { DEFAULT_NESTING_THRESHOLDS } from './core/detectors/nesting';
import { DEFAULT_CYCLOMATIC_THRESHOLDS } from './core/detectors/cyclomatic';
import { DEFAULT_COGNITIVE_THRESHOLDS } from './core/detectors/cognitive';
import { resolveLanguageForFile } from './languages';
import { EnergyViolation, SEVERITY } from './types';

function printUsage(): void {
    console.error('Usage: energy-state-cli <file.py|.fs|.fsx|.ts> [--medium-nesting N] [--high-nesting N] [--medium-cyclomatic N] [--high-cyclomatic N] [--medium-cognitive N] [--high-cognitive N]');
}

function parseArgs(argv: string[]) {
    const filePath = argv.find(arg => !arg.startsWith('--'));
    const flag = (name: string): number | undefined => {
        const index = argv.indexOf(`--${name}`);
        return index !== -1 ? Number(argv[index + 1]) : undefined;
    };

    return {
        filePath,
        nesting: {
            mediumThreshold: flag('medium-nesting'),
            highThreshold: flag('high-nesting')
        },
        cyclomatic: {
            mediumThreshold: flag('medium-cyclomatic'),
            highThreshold: flag('high-cyclomatic')
        },
        cognitive: {
            mediumThreshold: flag('medium-cognitive'),
            highThreshold: flag('high-cognitive')
        }
    };
}

type ThresholdOverride = { mediumThreshold?: number; highThreshold?: number };

// decision: returns undefined (not the detector's own default) when neither flag was passed —
// analyzeSource already falls back to each detector's own DEFAULT_*_THRESHOLDS when its entry is
// undefined, so resolving the default here too would just be a second place that default could
// drift out of sync with the detector module that owns it
function resolveThresholdOverride<T extends ThresholdOverride>(override: ThresholdOverride, defaults: T): T | undefined {
    if (override.mediumThreshold === undefined && override.highThreshold === undefined) {
        return undefined;
    }
    return {
        ...defaults,
        mediumThreshold: override.mediumThreshold ?? defaults.mediumThreshold,
        highThreshold: override.highThreshold ?? defaults.highThreshold
    };
}

function buildThresholds(parsed: ReturnType<typeof parseArgs>): AnalyzeThresholds {
    return {
        nesting: resolveThresholdOverride(parsed.nesting, DEFAULT_NESTING_THRESHOLDS),
        cyclomatic: resolveThresholdOverride(parsed.cyclomatic, DEFAULT_CYCLOMATIC_THRESHOLDS),
        cognitive: resolveThresholdOverride(parsed.cognitive, DEFAULT_COGNITIVE_THRESHOLDS)
    };
}

async function loadParser(adapter: LanguageAdapter) {
    await Parser.init();
    const parser = new Parser();
    const grammarPath = path.join(__dirname, '..', adapter.grammarPath);
    const grammar = await Language.load(grammarPath);
    parser.setLanguage(grammar);
    return parser;
}

async function main(): Promise<void> {
    const parsed = parseArgs(process.argv.slice(2));
    const { filePath } = parsed;

    if (!filePath) {
        printUsage();
        process.exit(2);
    }

    const adapter = resolveLanguageForFile(filePath);
    if (!adapter) {
        console.error(`Unsupported file type: ${filePath}`);
        printUsage();
        process.exit(2);
    }

    const sourceCode = fs.readFileSync(filePath, 'utf8');
    const parser = await loadParser(adapter);
    const tree = parser.parse(sourceCode);

    const violations: EnergyViolation[] = analyzeSource(sourceCode, tree, adapter, filePath, buildThresholds(parsed));

    console.log(JSON.stringify(violations, null, 2));

    const hasBlockingViolation = violations.some(v => v.severity === SEVERITY.HIGH || v.severity === SEVERITY.MEDIUM);
    process.exit(hasBlockingViolation ? 1 : 0);
}

main().catch(error => {
    console.error('energy-state-cli failed:', error);
    process.exit(1);
});
