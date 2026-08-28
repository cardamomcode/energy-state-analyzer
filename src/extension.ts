import * as vscode from 'vscode';
import * as path from 'path';
const { Parser, Language } = require('web-tree-sitter');

import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from './types';
import { analyzeSource } from './core/analyze';
import { createPositionLookup } from './core/position';
import { extractTypeInformation } from './core/pythonTypeInfo';
import { LanguageAdapter } from './core/language';
import { readAnalyzeThresholds, getEnergyColors, DEFAULT_ENERGY_COLORS } from './config';
import { isIgnored, loadIgnorePatterns } from './core/esaignore';
import { LANGUAGES } from './languages';
import { PYTHON } from './languages/python';

interface LoadedLanguage {
    adapter: LanguageAdapter;
    parser: any;
}

// One tree-sitter Parser per supported language, keyed by vscode languageId.
// decision: populated lazily (see getOrLoadLanguage) rather than up front for every
// registered language — grammars range from Python's 448KB to F#'s 12MB, and loading
// all of them at activation makes every window pay that cost even for a single-language
// project
let loadedLanguages: Map<string, LoadedLanguage>;
// Dedupes concurrent loads of the same not-yet-loaded language — onDidChangeTextDocument
// can fire again before the first getOrLoadLanguage call for that language resolves.
let inFlightLoads: Map<string, Promise<LoadedLanguage>>;
let extensionPath: string;

// Create diagnostics collection at module level
let diagnosticsCollection: vscode.DiagnosticCollection;

// Decoration types for different energy states
let highEnergyDecoration: vscode.TextEditorDecorationType;
let mediumEnergyDecoration: vscode.TextEditorDecorationType;
let lowEnergyDecoration: vscode.TextEditorDecorationType;

// decision: normalizes heatmap intensity per-violation (relative to the worst line in that function), not globally across the file — the darkest red always marks the line driving that function's complexity the most, regardless of how the function compares to others in the file
let complexityHeatDecorations: vscode.TextEditorDecorationType[];

export async function activate(context: vscode.ExtensionContext) {
    console.log('🚀 Activating Energy State Analyzer...');

    try {
        // Initialize Parser
        console.log('🔧 Initializing Parser...');
        await Parser.init();
        console.log('✅ Parser initialized');

        loadedLanguages = new Map();
        inFlightLoads = new Map();
        extensionPath = context.extensionPath;

        // Create decoration types
        createDecorations();
        console.log('🎨 Decoration types created');

        // Create diagnostics collection for Problems panel
        diagnosticsCollection = vscode.languages.createDiagnosticCollection('energyState');
        context.subscriptions.push(diagnosticsCollection);
        console.log('📋 Diagnostics collection created');

        // Register command
        const disposable = vscode.commands.registerCommand('energy-state-analyzer.analyze', () => {
            vscode.window.showInformationMessage('Energy State Analyzer: Manual analysis triggered!');
            void analyzeActiveEditor();
        });
        context.subscriptions.push(disposable);

        // Register event listeners
        vscode.window.onDidChangeActiveTextEditor(() => void analyzeActiveEditor());
        // tradeoff: re-parses and re-runs every detector on every keystroke rather than debouncing — keeps decorations and Problems-panel entries always in sync with the visible buffer, at the cost of re-analysis work the user never sees skipped
        vscode.workspace.onDidChangeTextDocument((event) => {
            if (event.document === vscode.window.activeTextEditor?.document) {
                void analyzeActiveEditor();
            }
        });
        vscode.workspace.onDidChangeConfiguration((event) => {
            if (event.affectsConfiguration('energyStateAnalyzer.colors')) {
                disposeDecorations();
                createDecorations();
            }
            if (event.affectsConfiguration('energyStateAnalyzer')) {
                void analyzeActiveEditor();
            }
        });

        // Clear diagnostics when document is closed
        vscode.workspace.onDidCloseTextDocument((document) => {
            if (document.languageId in LANGUAGES) {
                diagnosticsCollection.delete(document.uri);
            }
        });

        // Analyze current editor if open
        void analyzeActiveEditor();

        console.log('✅ Energy State Analyzer activated successfully!');
    } catch (error) {
        console.error('Failed to activate Energy State Analyzer:', error);
        vscode.window.showErrorMessage(`Energy State Analyzer failed to activate: ${error}`);
    }
}

// Increasing alpha steps for the complexity heatmap bands, darkest last. Not user-configurable
// — see the `decision:` note at their only use site in createDecorations.
const HEAT_BAND_ALPHAS = [0.1, 0.18, 0.28, 0.42];

const HEX_RADIX = 16;
const HEX_CHANNEL_WIDTH = 2;

// decision: parses user-supplied hex strings defensively and falls back to the built-in default on malformed input, since a bad `energyStateAnalyzer.colors.*` setting must not crash decoration setup
function hexToRgba(hex: string, alpha: number, fallback: string): string {
    const match = /^#?([0-9a-fA-F]{6})$/.exec(hex.trim());
    const digits = match ? match[1] : fallback.replace('#', '');
    const r = parseHexChannel(digits, 0);
    const g = parseHexChannel(digits, 1);
    const b = parseHexChannel(digits, 2);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function parseHexChannel(digits: string, channelIndex: number): number {
    const start = channelIndex * HEX_CHANNEL_WIDTH;
    return parseInt(digits.substring(start, start + HEX_CHANNEL_WIDTH), HEX_RADIX);
}

function createDecorations() {
    const colors = getEnergyColors();

    highEnergyDecoration = vscode.window.createTextEditorDecorationType({
        // Subtle background highlight that's still hoverable
        backgroundColor: hexToRgba(colors.highEnergy, colors.backgroundOpacity, DEFAULT_ENERGY_COLORS.highEnergy),
        borderRadius: '2px',
        gutterIconPath: createLightningIcon(colors.highEnergy),
        gutterIconSize: 'contain'
    });

    mediumEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: hexToRgba(colors.mediumEnergy, colors.backgroundOpacity, DEFAULT_ENERGY_COLORS.mediumEnergy),
        borderRadius: '2px',
        gutterIconPath: createLightningIcon(colors.mediumEnergy),
        gutterIconSize: 'contain'
    });

    lowEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: hexToRgba(colors.lowEnergy, colors.backgroundOpacity, DEFAULT_ENERGY_COLORS.lowEnergy),
        borderRadius: '2px',
        gutterIconPath: createLightningIcon(colors.lowEnergy),
        gutterIconSize: 'contain'
    });

    // decision: complexity heat bands carry no gutter icon — the function-level violation decoration already owns the gutter icon for that line range, so these bands only paint background intensity
    // decision: heat bands derive from the same highEnergy color as the gutter icon (four increasing alpha steps) instead of a separate setting, so the heatmap and the violation it belongs to always match — kept as a fixed constant rather than a config option for the same reason
    complexityHeatDecorations = HEAT_BAND_ALPHAS.map((alpha) =>
        vscode.window.createTextEditorDecorationType({
            backgroundColor: hexToRgba(colors.highEnergy, alpha, DEFAULT_ENERGY_COLORS.highEnergy)
        })
    );
}

function disposeDecorations() {
    highEnergyDecoration?.dispose();
    mediumEnergyDecoration?.dispose();
    lowEnergyDecoration?.dispose();
    complexityHeatDecorations?.forEach((decoration) => decoration.dispose());
}

// Create lightning bolt icon for energy violations

function createLightningIcon(color: string): vscode.Uri {
    const svg = `
    <svg width="16" height="16" xmlns="http://www.w3.org/2000/svg">
        <circle cx="8" cy="8" r="7" fill="${color}" opacity="0.95"/>
        <path d="M6 3 L10 8 L8.5 8 L10.5 13 L6.5 8 L8 8 Z" fill="white" stroke="white" stroke-width="0.3"/>
    </svg>`;
    const dataUri = `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
    return vscode.Uri.parse(dataUri);
}

// Loads and caches a language's grammar on first use, keyed by vscode languageId.
// decision: caches the in-flight load promise too, not just the settled result — without
// this, a second analyzeActiveEditor call for the same not-yet-loaded language (e.g. from
// a rapid-fire onDidChangeTextDocument) would kick off its own redundant Language.load
async function getOrLoadLanguage(languageId: string): Promise<LoadedLanguage | undefined> {
    const cached = loadedLanguages.get(languageId);
    if (cached) {
        return cached;
    }

    const adapter = LANGUAGES[languageId];
    if (!adapter) {
        return undefined;
    }

    let pending = inFlightLoads.get(languageId);
    if (!pending) {
        pending = (async () => {
            const grammarPath = path.join(extensionPath, adapter.grammarPath);
            console.log(`📁 Loading ${adapter.id} grammar:`, grammarPath);
            const grammar = await Language.load(grammarPath);
            const languageParser = new Parser();
            languageParser.setLanguage(grammar);
            const loaded: LoadedLanguage = { adapter, parser: languageParser };
            loadedLanguages.set(adapter.id, loaded);
            console.log(`✅ ${adapter.id} grammar loaded successfully`);
            return loaded;
        })();
        inFlightLoads.set(languageId, pending);
    }
    return pending;
}

// A document with no containing workspace folder (e.g. a file opened standalone) has
// nowhere to look for a `.esaignore`, so it's never treated as ignored.
//
// `includeFixtures` is an editor-only override for visually spot-checking detector
// fixtures (deliberately bad code under .esaignore, e.g. src/test/fixtures) without
// touching .esaignore itself, which the CLI/CI scan (src/cliModes.ts) always honors.
const INCLUDE_FIXTURES_DEFAULT = false;

function isDocumentIgnored(document: vscode.TextDocument): boolean {
    const workspaceFolder = vscode.workspace.getWorkspaceFolder(document.uri);
    if (!workspaceFolder) {
        return false;
    }
    const includeFixtures = vscode.workspace
        .getConfiguration('energyStateAnalyzer')
        .get<boolean>('includeFixtures', INCLUDE_FIXTURES_DEFAULT);
    if (includeFixtures) {
        return false;
    }
    const rootDir = workspaceFolder.uri.fsPath;
    const patterns = loadIgnorePatterns(rootDir);
    return isIgnored(document.fileName, rootDir, patterns);
}

async function analyzeActiveEditor() {
    const editor = vscode.window.activeTextEditor;
    console.log('🔍 Analyzing active editor...');

    if (!editor) {
        console.log('❌ No active editor found');
        return;
    }

    if (isDocumentIgnored(editor.document)) {
        console.log('🚫 Ignored by .esaignore:', editor.document.fileName);
        applyDecorations(editor, []);
        diagnosticsCollection.delete(editor.document.uri);
        return;
    }

    const loaded = await getOrLoadLanguage(editor.document.languageId);
    if (!loaded) {
        console.log('⚠️ Unsupported language:', editor.document.languageId);
        // Clear diagnostics for unsupported languages
        diagnosticsCollection.clear();
        return;
    }

    // decision: re-reads the active editor after the await above instead of trusting the
    // `editor` captured before it — the user may have switched tabs while the grammar loaded
    if (vscode.window.activeTextEditor?.document !== editor.document) {
        return;
    }

    console.log(`📄 Analyzing ${loaded.adapter.id} file:`, editor.document.fileName);
    const violations = analyzeDocument(editor.document, loaded);
    console.log('🔍 Found', violations.length, 'energy violations');

    // Apply both visual decorations AND problems panel
    applyDecorations(editor, violations);
    updateProblemsPanel(editor.document, violations);
}

function analyzeDocument(document: vscode.TextDocument, loaded: LoadedLanguage): EnergyViolation[] {
    const sourceCode = document.getText();

    try {
        const tree = loaded.parser.parse(sourceCode);
        const violations = analyzeSource(sourceCode, tree, loaded.adapter, document.fileName, readAnalyzeThresholds());

        // decision: extracts type information for Python only and only logs it — scaffolding for future features, not yet wired into any violation, so it deliberately does not affect the returned violations
        if (loaded.adapter.id === PYTHON.id) {
            const typeInfo = extractTypeInformation(tree, createPositionLookup(sourceCode));
            console.log('🔍 Found types:', typeInfo);
        }

        return violations;
    } catch (error) {
        console.error('Error analyzing document:', error);
        return [];
    }
}

// Fixed-width span used to highlight a flagged element (a magic value, a parameter) when there's
// no AST range to highlight instead — not user-configurable, since it's a rendering detail of
// this decoration rather than a detection threshold.
const ELEMENT_HIGHLIGHT_WIDTH = 15;

function applyDecorations(editor: vscode.TextEditor, violations: EnergyViolation[]) {
    const highEnergyRanges: vscode.DecorationOptions[] = [];
    const mediumEnergyRanges: vscode.DecorationOptions[] = [];
    const lowEnergyRanges: vscode.DecorationOptions[] = [];

    for (const violation of violations) {
        // decision: picks the decoration range by violation type rather than storing a range on EnergyViolation itself — coherence issues span the whole first line, nesting/complexity/cognitive issues span from the construct's start to end of line, and everything else highlights a fixed-width span at the violation's column
        let range: vscode.Range;
        const line = editor.document.lineAt(violation.line);

        if (violation.type === VIOLATION_TYPE.COHERENCE) {
            // Highlight entire first line for file-level issues
            range = new vscode.Range(violation.line, 0, violation.line, line.text.length);
        } else if (
            violation.type === VIOLATION_TYPE.NESTING ||
            violation.type === VIOLATION_TYPE.COMPLEXITY ||
            violation.type === VIOLATION_TYPE.COGNITIVE
        ) {
            // Highlight from function start to end of line
            const functionStart = line.text.search(/\S/); // Find first non-whitespace
            range = new vscode.Range(violation.line, functionStart, violation.line, line.text.length);
        } else {
            // For magic values and parameters, highlight the specific element
            const endColumn = Math.min(violation.column + ELEMENT_HIGHLIGHT_WIDTH, line.text.length);
            range = new vscode.Range(violation.line, violation.column, violation.line, endColumn);
        }

        const decoration: vscode.DecorationOptions = {
            range,
            hoverMessage: `🔋 Energy Violation: ${violation.message}`
        };

        switch (violation.severity) {
            case SEVERITY.HIGH:
                highEnergyRanges.push(decoration);
                break;
            case SEVERITY.MEDIUM:
                mediumEnergyRanges.push(decoration);
                break;
            case SEVERITY.LOW:
                lowEnergyRanges.push(decoration);
                break;
        }
    }

    editor.setDecorations(highEnergyDecoration, highEnergyRanges);
    editor.setDecorations(mediumEnergyDecoration, mediumEnergyRanges);
    editor.setDecorations(lowEnergyDecoration, lowEnergyRanges);

    applyComplexityHeat(editor, violations);
}

// invariant: heat intensity is normalized per-violation — the single worst line in a function is always the darkest band, regardless of how that function compares to others in the file
function computeHeatByLine(violations: EnergyViolation[]): Map<number, number> {
    const heatByLine = new Map<number, number>();

    for (const violation of violations) {
        if (!violation.hotspots || violation.hotspots.length === 0) {
            continue;
        }

        const maxWeight = Math.max(...violation.hotspots.map((hotspot) => hotspot.weight));
        if (maxWeight <= 0) {
            continue;
        }

        for (const hotspot of violation.hotspots) {
            const intensity = hotspot.weight / maxWeight;
            heatByLine.set(hotspot.line, Math.max(heatByLine.get(hotspot.line) ?? 0, intensity));
        }
    }

    return heatByLine;
}

// Paints a progressive heatmap (in the configured high-energy color) over the lines
// that actually drive a flagged function's complexity, so instead of just knowing
// "this function is complex" you can see exactly which branches to break apart first.
function applyComplexityHeat(editor: vscode.TextEditor, violations: EnergyViolation[]) {
    const heatByLine = computeHeatByLine(violations);
    const bandCount = complexityHeatDecorations.length;
    const bandRanges: vscode.Range[][] = complexityHeatDecorations.map(() => []);

    for (const [line, intensity] of heatByLine) {
        if (line < 0 || line >= editor.document.lineCount) {
            continue;
        }
        const bandIndex = Math.min(bandCount - 1, Math.floor(intensity * bandCount));
        const lineText = editor.document.lineAt(line).text;
        bandRanges[bandIndex].push(new vscode.Range(line, 0, line, lineText.length));
    }

    complexityHeatDecorations.forEach((decoration, index) => {
        editor.setDecorations(decoration, bandRanges[index]);
    });
}

function toDiagnosticSeverity(severity: string): vscode.DiagnosticSeverity {
    switch (severity) {
        case SEVERITY.HIGH:
            return vscode.DiagnosticSeverity.Error;
        case SEVERITY.MEDIUM:
            return vscode.DiagnosticSeverity.Warning;
        default:
            return vscode.DiagnosticSeverity.Information;
    }
}

function tagsForViolationType(type: string): vscode.DiagnosticTag[] {
    switch (type) {
        case VIOLATION_TYPE.NESTING:
            // decision: reuses DiagnosticTag.Unnecessary (fade/gray-out) for nesting violations — the closest built-in cue for "this structure could be flattened away"
            return [vscode.DiagnosticTag.Unnecessary];
        case VIOLATION_TYPE.COMPLEXITY:
        case VIOLATION_TYPE.COGNITIVE:
            // decision: reuses DiagnosticTag.Deprecated (strikethrough) as a visual cue for complexity violations — VS Code has no "high effort" tag, and Deprecated's strikethrough is the closest built-in signal for "this needs rework"
            return [vscode.DiagnosticTag.Deprecated];
        default:
            return [];
    }
}

// Fixed width for every Problems-panel diagnostic range — not user-configurable; see the
// decision note at its use site in buildLineDiagnostic below.
const DIAGNOSTIC_RANGE_WIDTH = 10;

function groupViolationsByLine(violations: EnergyViolation[]): Map<number, EnergyViolation[]> {
    const byLine = new Map<number, EnergyViolation[]>();
    for (const violation of violations) {
        const group = byLine.get(violation.line);
        if (group) {
            group.push(violation);
        } else {
            byLine.set(violation.line, [violation]);
        }
    }
    return byLine;
}

function buildLineDiagnostic(group: EnergyViolation[]): vscode.Diagnostic {
    // Sort so the highest-severity, then earliest-column violation leads the combined message
    const bySeverityThenColumn = [...group].sort(
        (a, b) => toDiagnosticSeverity(a.severity) - toDiagnosticSeverity(b.severity) || a.column - b.column
    );
    const lead = bySeverityThenColumn[0];

    // decision: uses a fixed-width range for every diagnostic regardless of violation type — the Problems panel only needs a clickable location, unlike applyDecorations' editor highlight which must visually match the flagged construct
    const range = new vscode.Range(lead.line, lead.column, lead.line, lead.column + DIAGNOSTIC_RANGE_WIDTH);

    const message =
        bySeverityThenColumn.length === 1 ? lead.message : bySeverityThenColumn.map((v) => v.message).join(' | ');

    const diagnostic = new vscode.Diagnostic(range, message, toDiagnosticSeverity(lead.severity));
    diagnostic.source = 'Energy State Analyzer';
    diagnostic.code = bySeverityThenColumn.map((v) => `energy-${v.type}`).join(',');

    const tags = bySeverityThenColumn.flatMap((v) => tagsForViolationType(v.type));
    if (tags.length > 0) {
        diagnostic.tags = tags;
    }

    return diagnostic;
}

// decision: groups violations by line before building diagnostics, rather than emitting one
// Diagnostic per violation — VS Code's inline "after-line" problem text shows only a single
// diagnostic's message per line (picked by its own severity/position heuristic), silently
// dropping the rest even though the hover popup correctly lists every diagnostic on that line.
// Merging same-line violations into one Diagnostic with a combined message means the inline
// text can no longer hide a violation the hover would otherwise reveal.
function updateProblemsPanel(document: vscode.TextDocument, violations: EnergyViolation[]) {
    const byLine = groupViolationsByLine(violations);
    const diagnostics = [...byLine.values()].map(buildLineDiagnostic);
    diagnosticsCollection.set(document.uri, diagnostics);
}

export function deactivate() {
    // Clean up decorations AND diagnostics
    highEnergyDecoration?.dispose();
    mediumEnergyDecoration?.dispose();
    lowEnergyDecoration?.dispose();
    diagnosticsCollection?.dispose();
}
