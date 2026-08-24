import * as vscode from 'vscode';
import * as path from 'path';
const { Parser, Language } = require('web-tree-sitter');

import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from './types';
import { analyzeSource } from './core/analyze';
import { LanguageAdapter } from './core/language';
import { NestingThresholds, DEFAULT_NESTING_THRESHOLDS } from './core/detectors/nesting';
import { CyclomaticThresholds, DEFAULT_CYCLOMATIC_THRESHOLDS } from './core/detectors/cyclomatic';
import { CognitiveThresholds, DEFAULT_COGNITIVE_THRESHOLDS } from './core/detectors/cognitive';
import { CoherenceThresholds, DEFAULT_COHERENCE_THRESHOLDS } from './core/detectors/coherence';
import { MatchOpportunityThresholds, DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS } from './core/detectors/matchOpportunity';
import { MagicValuesOptions, DEFAULT_MAGIC_VALUES_OPTIONS } from './core/detectors/magicValues';
import { LANGUAGES } from './languages';
import { PYTHON } from './languages/python';

interface LoadedLanguage {
    adapter: LanguageAdapter;
    parser: any;
}

// One tree-sitter Parser per supported language, keyed by vscode languageId.
let loadedLanguages: Map<string, LoadedLanguage>;

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
    vscode.window.showInformationMessage('Energy State Analyzer: Starting activation...');

    try {
        // Initialize Parser
        console.log('🔧 Initializing Parser...');
        await Parser.init();
        console.log('✅ Parser initialized');

        // Load every supported language's grammar up front.
        loadedLanguages = new Map();
        for (const adapter of Object.values(LANGUAGES)) {
            const grammarPath = path.join(context.extensionPath, adapter.grammarPath);
            console.log(`📁 Loading ${adapter.id} grammar:`, grammarPath);
            const grammar = await Language.load(grammarPath);
            const languageParser = new Parser();
            languageParser.setLanguage(grammar);
            loadedLanguages.set(adapter.id, { adapter, parser: languageParser });
            console.log(`✅ ${adapter.id} grammar loaded successfully`);
        }

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
            analyzeActiveEditor();
        });
        context.subscriptions.push(disposable);

        // Register event listeners
        vscode.window.onDidChangeActiveTextEditor(analyzeActiveEditor);
        // tradeoff: re-parses and re-runs every detector on every keystroke rather than debouncing — keeps decorations and Problems-panel entries always in sync with the visible buffer, at the cost of re-analysis work the user never sees skipped
        vscode.workspace.onDidChangeTextDocument(event => {
            if (event.document === vscode.window.activeTextEditor?.document) {
                analyzeActiveEditor();
            }
        });
        vscode.workspace.onDidChangeConfiguration(event => {
            if (event.affectsConfiguration('energyStateAnalyzer.colors')) {
                disposeDecorations();
                createDecorations();
            }
            if (event.affectsConfiguration('energyStateAnalyzer')) {
                analyzeActiveEditor();
            }
        });

        // Clear diagnostics when document is closed
        vscode.workspace.onDidCloseTextDocument(document => {
            if (loadedLanguages.has(document.languageId)) {
                diagnosticsCollection.delete(document.uri);
            }
        });

        // Analyze current editor if open
        analyzeActiveEditor();

        console.log('✅ Energy State Analyzer activated successfully!');
        vscode.window.showInformationMessage('Energy State Analyzer: Ready! Open a Python, F#, or TypeScript file to see energy analysis.');

    } catch (error) {
        console.error('Failed to activate Energy State Analyzer:', error);
        vscode.window.showErrorMessage(`Energy State Analyzer failed to activate: ${error}`);
    }
}

interface EnergyColors {
    highEnergy: string;
    mediumEnergy: string;
    lowEnergy: string;
    backgroundOpacity: number;
}

const DEFAULT_ENERGY_COLORS: EnergyColors = {
    highEnergy: '#fb8500',
    mediumEnergy: '#ffb703',
    lowEnergy: '#99dd99',
    backgroundOpacity: 0.1
};

function getEnergyColors(): EnergyColors {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.colors');
    return {
        highEnergy: config.get('highEnergy', DEFAULT_ENERGY_COLORS.highEnergy),
        mediumEnergy: config.get('mediumEnergy', DEFAULT_ENERGY_COLORS.mediumEnergy),
        lowEnergy: config.get('lowEnergy', DEFAULT_ENERGY_COLORS.lowEnergy),
        backgroundOpacity: config.get('backgroundOpacity', DEFAULT_ENERGY_COLORS.backgroundOpacity)
    };
}

// decision: parses user-supplied hex strings defensively and falls back to the built-in default on malformed input, since a bad `energyStateAnalyzer.colors.*` setting must not crash decoration setup
function hexToRgba(hex: string, alpha: number, fallback: string): string {
    const match = /^#?([0-9a-fA-F]{6})$/.exec(hex.trim());
    const digits = match ? match[1] : fallback.replace('#', '');
    const r = parseInt(digits.substring(0, 2), 16);
    const g = parseInt(digits.substring(2, 4), 16);
    const b = parseInt(digits.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
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
    // decision: heat bands derive from the same highEnergy color as the gutter icon (four increasing alpha steps) instead of a separate setting, so the heatmap and the violation it belongs to always match
    complexityHeatDecorations = [0.10, 0.18, 0.28, 0.42].map(alpha =>
        vscode.window.createTextEditorDecorationType({
            backgroundColor: hexToRgba(colors.highEnergy, alpha, DEFAULT_ENERGY_COLORS.highEnergy)
        })
    );
}

function disposeDecorations() {
    highEnergyDecoration?.dispose();
    mediumEnergyDecoration?.dispose();
    lowEnergyDecoration?.dispose();
    complexityHeatDecorations?.forEach(decoration => decoration.dispose());
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


function analyzeActiveEditor() {
    const editor = vscode.window.activeTextEditor;
    console.log('🔍 Analyzing active editor...');

    if (!editor) {
        console.log('❌ No active editor found');
        return;
    }

    const loaded = loadedLanguages.get(editor.document.languageId);
    if (!loaded) {
        console.log('⚠️ Unsupported language:', editor.document.languageId);
        // Clear diagnostics for unsupported languages
        diagnosticsCollection.clear();
        return;
    }

    console.log(`📄 Analyzing ${loaded.adapter.id} file:`, editor.document.fileName);
    const violations = analyzeDocument(editor.document, loaded);
    console.log('🔍 Found', violations.length, 'energy violations');

    // Apply both visual decorations AND problems panel
    applyDecorations(editor, violations);
    updateProblemsPanel(editor.document, violations);
}

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
        maxLargeFunctions: config.get('maxLargeFunctions', DEFAULT_COHERENCE_THRESHOLDS.maxLargeFunctions)
    };
}

function getMatchOpportunityThresholds(): MatchOpportunityThresholds {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.matchOpportunity');
    return {
        minBranches: config.get('minBranches', DEFAULT_MATCH_OPPORTUNITY_THRESHOLDS.minBranches)
    };
}

function getMagicValuesOptions(): MagicValuesOptions {
    const config = vscode.workspace.getConfiguration('energyStateAnalyzer.magicValues');
    return {
        enabled: config.get('enabled', DEFAULT_MAGIC_VALUES_OPTIONS.enabled)
    };
}

function analyzeDocument(document: vscode.TextDocument, loaded: LoadedLanguage): EnergyViolation[] {
    const sourceCode = document.getText();

    try {
        const tree = loaded.parser.parse(sourceCode);
        const violations = analyzeSource(sourceCode, tree, loaded.adapter, document.fileName, {
            nesting: getNestingThresholds(),
            cyclomatic: getCyclomaticThresholds(),
            cognitive: getCognitiveThresholds(),
            coherence: getCoherenceThresholds(),
            matchOpportunity: getMatchOpportunityThresholds(),
            magicValues: getMagicValuesOptions()
        });

        // decision: extracts type information for Python only and only logs it — scaffolding for future features, not yet wired into any violation, so it deliberately does not affect the returned violations
        if (loaded.adapter.id === PYTHON.id) {
            const typeInfo = extractTypeInformation(tree, document);
            console.log('🔍 Found types:', typeInfo);
        }

        return violations;
    } catch (error) {
        console.error('Error analyzing document:', error);
        return [];
    }
}

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
        } else if (violation.type === VIOLATION_TYPE.NESTING || violation.type === VIOLATION_TYPE.COMPLEXITY || violation.type === VIOLATION_TYPE.COGNITIVE) {
            // Highlight from function start to end of line
            const functionStart = line.text.search(/\S/); // Find first non-whitespace
            range = new vscode.Range(violation.line, functionStart, violation.line, line.text.length);
        } else {
            // For magic values and parameters, highlight the specific element
            const endColumn = Math.min(violation.column + 15, line.text.length);
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

// Paints a progressive red heatmap over the lines that actually drive a
// flagged function's complexity, so instead of just knowing "this function
// is complex" you can see exactly which branches to break apart first.
//
// invariant: heat intensity is normalized per-violation — the single worst line in a function is always the darkest band, regardless of how that function compares to others in the file
function applyComplexityHeat(editor: vscode.TextEditor, violations: EnergyViolation[]) {
    const heatByLine = new Map<number, number>();

    for (const violation of violations) {
        if (!violation.hotspots || violation.hotspots.length === 0) {
            continue;
        }

        const maxWeight = Math.max(...violation.hotspots.map(hotspot => hotspot.weight));
        if (maxWeight <= 0) {
            continue;
        }

        for (const hotspot of violation.hotspots) {
            const intensity = hotspot.weight / maxWeight;
            heatByLine.set(hotspot.line, Math.max(heatByLine.get(hotspot.line) ?? 0, intensity));
        }
    }

    const bandRanges: vscode.Range[][] = complexityHeatDecorations.map(() => []);

    for (const [line, intensity] of heatByLine) {
        if (line < 0 || line >= editor.document.lineCount) {
            continue;
        }
        const bandIndex = Math.min(
            complexityHeatDecorations.length - 1,
            Math.floor(intensity * complexityHeatDecorations.length)
        );
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

// decision: groups violations by line before building diagnostics, rather than emitting one
// Diagnostic per violation — VS Code's inline "after-line" problem text shows only a single
// diagnostic's message per line (picked by its own severity/position heuristic), silently
// dropping the rest even though the hover popup correctly lists every diagnostic on that line.
// Merging same-line violations into one Diagnostic with a combined message means the inline
// text can no longer hide a violation the hover would otherwise reveal.
function updateProblemsPanel(document: vscode.TextDocument, violations: EnergyViolation[]) {
    const byLine = new Map<number, EnergyViolation[]>();
    for (const violation of violations) {
        const group = byLine.get(violation.line);
        if (group) {
            group.push(violation);
        } else {
            byLine.set(violation.line, [violation]);
        }
    }

    const diagnostics: vscode.Diagnostic[] = [];
    for (const group of byLine.values()) {
        // Sort so the highest-severity, then earliest-column violation leads the combined message
        const bySeverityThenColumn = [...group].sort((a, b) =>
            toDiagnosticSeverity(a.severity) - toDiagnosticSeverity(b.severity) || a.column - b.column
        );
        const lead = bySeverityThenColumn[0];

        // decision: uses a fixed 10-column-wide range for every diagnostic regardless of violation type — the Problems panel only needs a clickable location, unlike applyDecorations' editor highlight which must visually match the flagged construct
        const range = new vscode.Range(
            lead.line, lead.column,
            lead.line, lead.column + 10
        );

        const message = bySeverityThenColumn.length === 1
            ? lead.message
            : bySeverityThenColumn.map(v => v.message).join(' | ');

        const diagnostic = new vscode.Diagnostic(
            range,
            message,
            toDiagnosticSeverity(lead.severity)
        );

        diagnostic.source = 'Energy State Analyzer';
        diagnostic.code = bySeverityThenColumn.map(v => `energy-${v.type}`).join(',');
        const tags = bySeverityThenColumn.flatMap(v => tagsForViolationType(v.type));
        if (tags.length > 0) {
            diagnostic.tags = tags;
        }

        diagnostics.push(diagnostic);
    }

    // Update the Problems panel
    diagnosticsCollection.set(document.uri, diagnostics);
}

// Type information extraction from AST
interface TypeInfo {
    functions: FunctionTypeInfo[];
    variables: VariableTypeInfo[];
    classes: ClassTypeInfo[];
    imports: ImportInfo[];
}

interface FunctionTypeInfo {
    name: string;
    line: number;
    parameters: ParameterTypeInfo[];
    returnType: string | null;
}

interface ParameterTypeInfo {
    name: string;
    type: string | null;
    hasDefault: boolean;
}

interface VariableTypeInfo {
    name: string;
    type: string;
    line: number;
}

interface ClassTypeInfo {
    name: string;
    line: number;
    baseClasses: string[];
    isTypedDict: boolean;
    fields: VariableTypeInfo[];
}

interface ImportInfo {
    module: string;
    items: string[];
    line: number;
}

function extractTypeInformation(tree: any, document: vscode.TextDocument): TypeInfo {
    const typeInfo: TypeInfo = {
        functions: [],
        variables: [],
        classes: [],
        imports: []
    };

    function traverse(node: any) {
        switch (node.type) {
            case 'function_definition':
                typeInfo.functions.push(extractFunctionTypeInfo(node, document));
                break;
            case 'class_definition':
                typeInfo.classes.push(extractClassTypeInfo(node, document));
                break;
            case 'assignment':
                const varInfo = extractVariableTypeInfo(node, document);
                if (varInfo) {
                    typeInfo.variables.push(varInfo);
                }
                break;
            case 'import_statement':
            case 'import_from_statement':
                typeInfo.imports.push(extractImportInfo(node, document));
                break;
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return typeInfo;
}

function extractFunctionTypeInfo(node: any, document: vscode.TextDocument): FunctionTypeInfo {
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const parametersNode = node.children.find((child: any) => child.type === 'parameters');

    const returnType = extractReturnTypeAnnotation(node.children);
    const parameters = parametersNode ? extractParameters(parametersNode) : [];

    const position = document.positionAt(node.startIndex);
    return {
        name: nameNode?.text || 'unknown',
        line: position.line,
        parameters,
        returnType
    };
}

function extractReturnTypeAnnotation(children: any[]): string | null {
    const arrowIndex = children.findIndex((child: any) => child.text === '->');
    if (arrowIndex === -1 || arrowIndex + 1 >= children.length) {
        return null;
    }

    const returnTypeNode = children[arrowIndex + 1];
    return returnTypeNode.type === 'type' ? extractTypeString(returnTypeNode) : null;
}

function extractParameters(parametersNode: any): ParameterTypeInfo[] {
    const parameters: ParameterTypeInfo[] = [];
    for (const child of parametersNode.children) {
        if (child.type === 'typed_parameter') {
            parameters.push(extractParameterTypeInfo(child));
        } else if (child.type === 'default_parameter') {
            parameters.push(extractDefaultParameterTypeInfo(child));
        } else if (child.type === 'identifier') {
            // Untyped parameter
            parameters.push({ name: child.text, type: null, hasDefault: false });
        }
    }
    return parameters;
}

function extractParameterTypeInfo(node: any): ParameterTypeInfo {
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const typeNode = node.children.find((child: any) => child.type === 'type');

    return {
        name: nameNode?.text || 'unknown',
        type: typeNode ? extractTypeString(typeNode) : null,
        hasDefault: false
    };
}

function extractDefaultParameterTypeInfo(node: any): ParameterTypeInfo {
    // Default parameters might have type annotations too
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const typeNode = node.children.find((child: any) => child.type === 'type');

    return {
        name: nameNode?.text || 'unknown',
        type: typeNode ? extractTypeString(typeNode) : null,
        hasDefault: true
    };
}

function extractVariableTypeInfo(node: any, document: vscode.TextDocument): VariableTypeInfo | null {
    // Look for assignments with type annotations: x: int = 5
    const identifierNode = node.children.find((child: any) => child.type === 'identifier');
    const typeNode = node.children.find((child: any) => child.type === 'type');

    if (identifierNode && typeNode) {
        const position = document.positionAt(node.startIndex);
        return {
            name: identifierNode.text,
            type: extractTypeString(typeNode),
            line: position.line
        };
    }

    return null;
}

function extractClassTypeInfo(node: any, document: vscode.TextDocument): ClassTypeInfo {
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const argumentListNode = node.children.find((child: any) => child.type === 'argument_list');

    const baseClasses = extractBaseClasses(argumentListNode);
    const isTypedDict = baseClasses.includes('TypedDict');

    const blockNode = node.children.find((child: any) => child.type === 'block');
    const fields = isTypedDict ? extractTypedDictFields(blockNode, document) : [];

    const position = document.positionAt(node.startIndex);
    return {
        name: nameNode?.text || 'unknown',
        line: position.line,
        baseClasses,
        isTypedDict,
        fields
    };
}

function extractBaseClasses(argumentListNode: any): string[] {
    if (!argumentListNode) {
        return [];
    }

    return argumentListNode.children
        .filter((child: any) => child.type === 'identifier')
        .map((child: any) => child.text);
}

function extractTypedDictFields(blockNode: any, document: vscode.TextDocument): VariableTypeInfo[] {
    if (!blockNode) {
        return [];
    }

    const fields: VariableTypeInfo[] = [];
    for (const child of blockNode.children) {
        if (child.type !== 'expression_statement') {
            continue;
        }
        const assignment = child.children.find((grandchild: any) => grandchild.type === 'assignment');
        if (!assignment) {
            continue;
        }
        const fieldInfo = extractVariableTypeInfo(assignment, document);
        if (fieldInfo) {
            fields.push(fieldInfo);
        }
    }
    return fields;
}

function extractImportInfo(node: any, document: vscode.TextDocument): ImportInfo {
    const line = document.positionAt(node.startIndex).line;

    if (node.type === 'import_statement') {
        return extractPlainImportInfo(node, line);
    }
    if (node.type === 'import_from_statement') {
        return extractFromImportInfo(node, line);
    }

    return { module: '', items: [], line };
}

function extractPlainImportInfo(node: any, line: number): ImportInfo {
    // import module1, module2
    const items = node.children
        .filter((child: any) => child.type === 'dotted_name' || child.type === 'identifier')
        .map((child: any) => child.text);

    return { module: items[0] || '', items, line };
}

function extractFromImportInfo(node: any, line: number): ImportInfo {
    // from module import item1, item2
    const fromIndex = node.children.findIndex((child: any) => child.text === 'from');
    const importIndex = node.children.findIndex((child: any) => child.text === 'import');

    if (fromIndex === -1 || importIndex === -1) {
        return { module: '', items: [], line };
    }

    const module = findImportModuleName(node.children, fromIndex, importIndex);
    const items = collectImportedItems(node.children, importIndex);

    return { module, items, line };
}

function findImportModuleName(children: any[], fromIndex: number, importIndex: number): string {
    for (let i = fromIndex + 1; i < importIndex; i++) {
        const child = children[i];
        if (child.type === 'dotted_name' || child.type === 'identifier') {
            return child.text;
        }
    }
    return '';
}

function collectImportedItems(children: any[], importIndex: number): string[] {
    const items: string[] = [];
    for (let i = importIndex + 1; i < children.length; i++) {
        const child = children[i];
        if (child.type === 'identifier') {
            items.push(child.text);
        }
    }
    return items;
}

function extractTypeString(typeNode: any): string {
    if (typeNode.type !== 'type' || typeNode.children.length !== 1) {
        return typeNode.text || 'unknown';
    }

    const child = typeNode.children[0];
    if (child.type === 'generic_type') {
        return extractGenericTypeString(child);
    }
    if (child.type === 'identifier') {
        return child.text;
    }

    return typeNode.text || 'unknown';
}

function extractGenericTypeString(genericTypeNode: any): string {
    const baseType = genericTypeNode.children.find((child: any) => child.type === 'identifier');
    const typeParameterNode = genericTypeNode.children.find((child: any) => child.type === 'type_parameter');

    if (baseType && typeParameterNode) {
        const params: string[] = [];
        for (const child of typeParameterNode.children) {
            if (child.type === 'type') {
                params.push(extractTypeString(child));
            }
        }
        return `${baseType.text}[${params.join(', ')}]`;
    }

    return genericTypeNode.text || 'unknown';
}

export function deactivate() {
    // Clean up decorations AND diagnostics
    highEnergyDecoration?.dispose();
    mediumEnergyDecoration?.dispose();
    lowEnergyDecoration?.dispose();
    diagnosticsCollection?.dispose();
}