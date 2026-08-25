#!/usr/bin/env node
// Headless entry point: run the same detectors the extension uses, without
// vscode, so an external process (e.g. an AI coding agent or a CI job) can
// gate on complexity without opening an editor.
//
// decision: exits 1 when any medium-or-high violation is found, not just high — CI/agent gating wants a single boolean signal, and severity is already visible in the emitted JSON for anyone who wants to discriminate further
// invariant: `energy-state-cli <single-file>` with no other flags keeps printing the flat
// EnergyViolation[] JSON array it always has — this is the published npm CLI contract
// (README's "Command-Line Usage"), and scan/diff mode are additive, not a replacement for it
import * as fs from 'fs';

import { AnalyzeThresholds } from './core/analyze';
import { DEFAULT_NESTING_THRESHOLDS } from './core/detectors/nesting';
import { DEFAULT_CYCLOMATIC_THRESHOLDS } from './core/detectors/cyclomatic';
import { DEFAULT_COGNITIVE_THRESHOLDS } from './core/detectors/cognitive';
import { printUsage, runDiff, runLegacySingleFile, runScan, ReportFormat } from './cliModes';

// decision: every flag recognized here (including --base-ref/--report) takes exactly one
// value, so a positional path list can be recovered by skipping each `--flag value` pair
// rather than needing a real argument-parsing library
const VALUE_FLAGS = ['base-ref', 'report', 'medium-nesting', 'high-nesting', 'medium-cyclomatic', 'high-cyclomatic', 'medium-cognitive', 'high-cognitive'];

function parseArgs(argv: string[]) {
    const paths: string[] = [];
    const flagValues = new Map<string, string>();

    for (let i = 0; i < argv.length; i++) {
        const arg = argv[i];
        if (arg.startsWith('--')) {
            const name = arg.slice(2);
            if (VALUE_FLAGS.includes(name)) {
                flagValues.set(name, argv[i + 1]);
                i++;
            }
        } else {
            paths.push(arg);
        }
    }

    const flag = (name: string): string | undefined => flagValues.get(name);
    const numberFlag = (name: string): number | undefined => {
        const value = flag(name);
        return value !== undefined ? Number(value) : undefined;
    };

    return {
        paths,
        baseRef: flag('base-ref'),
        report: (flag('report') as ReportFormat | undefined),
        nesting: {
            mediumThreshold: numberFlag('medium-nesting'),
            highThreshold: numberFlag('high-nesting')
        },
        cyclomatic: {
            mediumThreshold: numberFlag('medium-cyclomatic'),
            highThreshold: numberFlag('high-cyclomatic')
        },
        cognitive: {
            mediumThreshold: numberFlag('medium-cognitive'),
            highThreshold: numberFlag('high-cognitive')
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

async function main(): Promise<void> {
    const parsed = parseArgs(process.argv.slice(2));
    const thresholds = buildThresholds(parsed);
    const reportFormat: ReportFormat = parsed.report ?? (parsed.baseRef || parsed.paths.length !== 1 ? 'md' : 'json');

    if (parsed.baseRef) {
        await runDiff(parsed.baseRef, parsed.paths, thresholds, reportFormat);
        return;
    }

    if (parsed.paths.length === 0) {
        printUsage();
        process.exit(2);
    }

    if (parsed.paths.length === 1 && fs.existsSync(parsed.paths[0]) && fs.statSync(parsed.paths[0]).isFile() && !parsed.report) {
        await runLegacySingleFile(parsed.paths[0], thresholds);
        return;
    }

    await runScan(parsed.paths, thresholds, reportFormat);
}

main().catch(error => {
    console.error('energy-state-cli failed:', error);
    process.exit(1);
});
