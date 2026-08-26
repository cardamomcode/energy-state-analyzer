// Execution engine for each CLI invocation shape (single file, scan, diff) — split out of
// cli.ts so argument parsing and mode execution are two separate, independently-readable
// concerns (the coherence detector flagged cli.ts itself for mixing both in one file).
import * as fs from 'fs';
import * as path from 'path';
import { execFileSync } from 'child_process';
const { Parser, Language } = require('web-tree-sitter');

import { analyzeSource, AnalyzeThresholds } from './core/analyze';
import { LanguageAdapter } from './core/language';
import { resolveLanguageForFile } from './languages';
import { resolveSupportedFiles } from './core/scan';
import { isIgnored, loadIgnorePatterns } from './core/esaignore';
import { FileResult, FileSummary, hasBlockingViolations, renderDiffMarkdown, renderHumanReport, renderMarkdownReport, summarize, summarizeFile, diffSummaries } from './core/report';
import { EnergyViolation, SEVERITY } from './types';

export type ReportFormat = 'json' | 'md' | 'human';

export function printUsage(): void {
    console.error('Usage: energy-state-cli <file.py|.fs|.fsx|.ts> [thresholds...]');
    console.error('       energy-state-cli <path...> [--report json|md|human] [thresholds...]              (scan a directory/subtree)');
    console.error('       energy-state-cli --base-ref <ref> [<path...>] [--report json|md] [thresholds...]  (diff PR head against a base ref)');
    console.error('Thresholds: --medium-nesting N --high-nesting N --medium-cyclomatic N --high-cyclomatic N --medium-cognitive N --high-cognitive N');
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

export async function runLegacySingleFile(filePath: string, thresholds: AnalyzeThresholds): Promise<void> {
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

export async function runScan(paths: string[], thresholds: AnalyzeThresholds, reportFormat: ReportFormat): Promise<void> {
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
        // decision: pipes stderr instead of inheriting it — git's own "fatal: path ... exists
        // on disk, but not in <ref>" would otherwise print to the CLI's stderr on every new
        // file, duplicating (and outnumbering) our own, already-informative message below
        return execFileSync('git', ['show', `${ref}:${filePath}`], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
    } catch (error) {
        console.error(`energy-state-cli: could not read ${filePath} at ${ref} (new file or rename) — treating as new`);
        return null;
    }
}

export async function runDiff(baseRef: string, explicitPaths: string[], thresholds: AnalyzeThresholds, reportFormat: ReportFormat): Promise<void> {
    const rootDir = process.cwd();
    const ignorePatterns = loadIgnorePatterns(rootDir);
    const changedFiles = (explicitPaths.length > 0 ? explicitPaths : changedFilesFromGit(baseRef))
        .filter(filePath => resolveLanguageForFile(filePath) && fs.existsSync(filePath))
        .filter(filePath => !isIgnored(path.resolve(filePath), rootDir, ignorePatterns));

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

    // decision: diff-mode's exit code reflects whether anything in the diff *worsened*, not
    // "does head have any medium/high violation" (scan/legacy mode's rule) — a PR touching a
    // file that already carried debt, or a genuinely new file, should not fail this check on
    // pre-existing severity alone; only a file this PR made worse should block it
    process.exit(entries.some(entry => entry.status === 'worsened') ? 1 : 0);
}
