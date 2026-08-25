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
import * as path from 'path';
import { execFileSync } from 'child_process';
const { Parser, Language } = require('web-tree-sitter');

import { analyzeSource, AnalyzeThresholds } from './core/analyze';
import { LanguageAdapter } from './core/language';
import { DEFAULT_NESTING_THRESHOLDS } from './core/detectors/nesting';
import { DEFAULT_CYCLOMATIC_THRESHOLDS } from './core/detectors/cyclomatic';
import { DEFAULT_COGNITIVE_THRESHOLDS } from './core/detectors/cognitive';
import { resolveLanguageForFile } from './languages';
import { resolveSupportedFiles } from './core/scan';
import { FileResult, FileSummary, hasBlockingViolations, renderDiffMarkdown, renderHumanReport, renderMarkdownReport, summarize, summarizeFile, diffSummaries } from './core/report';
import { EnergyViolation, SEVERITY } from './types';

function printUsage(): void {
    console.error('Usage: energy-state-cli <file.py|.fs|.fsx|.ts> [thresholds...]');
    console.error('       energy-state-cli <path...> [--report json|md|human] [thresholds...]              (scan a directory/subtree)');
    console.error('       energy-state-cli --base-ref <ref> [<path...>] [--report json|md] [thresholds...]  (diff PR head against a base ref)');
    console.error('Thresholds: --medium-nesting N --high-nesting N --medium-cyclomatic N --high-cyclomatic N --medium-cognitive N --high-cognitive N');
}

type ReportFormat = 'json' | 'md' | 'human';

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

// decision: caches one parser instance per language adapter — scan/diff mode analyze many
// files, and re-running Parser.init()/Language.load() per file would be wasteful I/O
const parserCache = new Map<LanguageAdapter, any>();

async function loadParser(adapter: LanguageAdapter) {
    const cached = parserCache.get(adapter);
    if (cached) {
        return cached;
    }
    await Parser.init();
    const parser = new Parser();
    const grammarPath = path.join(__dirname, '..', adapter.grammarPath);
    const grammar = await Language.load(grammarPath);
    parser.setLanguage(grammar);
    parserCache.set(adapter, parser);
    return parser;
}

async function analyzeFile(filePath: string, sourceCode: string, thresholds: AnalyzeThresholds): Promise<EnergyViolation[]> {
    const adapter = resolveLanguageForFile(filePath);
    if (!adapter) {
        return [];
    }
    const parser = await loadParser(adapter);
    const tree = parser.parse(sourceCode);
    return analyzeSource(sourceCode, tree, adapter, filePath, thresholds);
}

function exitForCounts(counts: { low: number; medium: number; high: number }): never {
    process.exit(hasBlockingViolations(counts) ? 1 : 0);
}

async function runLegacySingleFile(filePath: string, thresholds: AnalyzeThresholds): Promise<void> {
    const adapter = resolveLanguageForFile(filePath);
    if (!adapter) {
        console.error(`Unsupported file type: ${filePath}`);
        printUsage();
        process.exit(2);
    }

    const sourceCode = fs.readFileSync(filePath, 'utf8');
    const violations = await analyzeFile(filePath, sourceCode, thresholds);

    console.log(JSON.stringify(violations, null, 2));

    const hasBlockingViolation = violations.some(v => v.severity === SEVERITY.HIGH || v.severity === SEVERITY.MEDIUM);
    process.exit(hasBlockingViolation ? 1 : 0);
}

async function runScan(paths: string[], thresholds: AnalyzeThresholds, reportFormat: ReportFormat): Promise<void> {
    const files = resolveSupportedFiles(paths);
    const results: FileResult[] = [];

    for (const filePath of files) {
        const sourceCode = fs.readFileSync(filePath, 'utf8');
        const violations = await analyzeFile(filePath, sourceCode, thresholds);
        // decision: reports the path relative to cwd — resolveSupportedFiles resolves
        // absolute paths internally for reliable dedup, but a repo-wide report reads far
        // better as "src/foo.ts" than a full filesystem path repeated on every row
        results.push({ filePath: path.relative(process.cwd(), filePath), violations });
    }

    const summary = summarize(results);
    if (reportFormat === 'human') {
        console.log(renderHumanReport(results));
    } else if (reportFormat === 'md') {
        console.log(renderMarkdownReport(summary));
    } else {
        console.log(JSON.stringify(summary, null, 2));
    }
    exitForCounts(summary.totalCounts);
}

function changedFilesFromGit(baseRef: string): string[] {
    const output = execFileSync('git', ['diff', '--name-only', '--diff-filter=d', `${baseRef}...HEAD`], { encoding: 'utf8' });
    return output.split('\n').map(line => line.trim()).filter(Boolean);
}

// decision: returns null (not throwing) when the file doesn't exist at baseRef — a newly
// added file has no base content, which is a normal "new file" case, not an error
function readAtRef(ref: string, filePath: string): string | null {
    try {
        return execFileSync('git', ['show', `${ref}:${filePath}`], { encoding: 'utf8' });
    } catch (error) {
        console.error(`energy-state-cli: could not read ${filePath} at ${ref} (new file or rename) — treating as new`);
        return null;
    }
}

async function runDiff(baseRef: string, explicitPaths: string[], thresholds: AnalyzeThresholds, reportFormat: ReportFormat): Promise<void> {
    const changedFiles = (explicitPaths.length > 0 ? explicitPaths : changedFilesFromGit(baseRef))
        .filter(filePath => resolveLanguageForFile(filePath) && fs.existsSync(filePath));

    const headSummaries: FileSummary[] = [];
    const baseSummaries: FileSummary[] = [];

    for (const filePath of changedFiles) {
        const headSource = fs.readFileSync(filePath, 'utf8');
        const headViolations = await analyzeFile(filePath, headSource, thresholds);
        headSummaries.push(summarizeFile({ filePath, violations: headViolations }));

        const baseSource = readAtRef(baseRef, filePath);
        if (baseSource !== null) {
            const baseViolations = await analyzeFile(filePath, baseSource, thresholds);
            baseSummaries.push(summarizeFile({ filePath, violations: baseViolations }));
        }
    }

    const entries = diffSummaries(baseSummaries, headSummaries);
    console.log(reportFormat === 'md' ? renderDiffMarkdown(entries, baseRef) : JSON.stringify(entries, null, 2));

    // decision: diff-mode's exit code reflects head-side medium/high violations, the same
    // rule as scan/legacy mode — "did it get worse" is visible in the rendered diff, not
    // encoded as a second exit-code contract
    const headTotals = { low: 0, medium: 0, high: 0 };
    for (const file of headSummaries) {
        headTotals.high += file.counts.high;
        headTotals.medium += file.counts.medium;
        headTotals.low += file.counts.low;
    }
    exitForCounts(headTotals);
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
