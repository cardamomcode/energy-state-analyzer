import * as vscode from 'vscode';
import * as path from 'path';
const { Parser, Language } = require('web-tree-sitter');

import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from './types';
import { analyzeSource } from './core/analyze';
import { CyclomaticThresholds, DEFAULT_CYCLOMATIC_THRESHOLDS } from './core/detectors/cyclomatic';
import { CognitiveThresholds, DEFAULT_COGNITIVE_THRESHOLDS } from './core/detectors/cognitive';
import { PYTHON } from './languages/python';

let parser: any;
let pythonLanguage: any;

// Create diagnostics collection at module level
let diagnosticsCollection: vscode.DiagnosticCollection;

// Decoration types for different energy states
let highEnergyDecoration: vscode.TextEditorDecorationType;
let mediumEnergyDecoration: vscode.TextEditorDecorationType;
let lowEnergyDecoration: vscode.TextEditorDecorationType;

// Progressive heat decorations for complexity hotspot lines, from mildest to
// most severe. Intensity is computed per-violation (relative to the worst
// line in that function), so the darkest red always marks the line driving
// the complexity the most.
let complexityHeatDecorations: vscode.TextEditorDecorationType[];

export async function activate(context: vscode.ExtensionContext) {
    console.log('🚀 Activating Energy State Analyzer...');
    vscode.window.showInformationMessage('Energy State Analyzer: Starting activation...');

    try {
        // Initialize Parser
        console.log('🔧 Initializing Parser...');
        await Parser.init();
        console.log('✅ Parser initialized');

        // Create parser
        parser = new Parser();
        console.log('🔧 Parser created');

        // Load Python grammar
        const grammarPath = path.join(context.extensionPath, PYTHON.grammarPath);
        console.log('📁 Grammar path:', grammarPath);
        pythonLanguage = await Language.load(grammarPath);
        console.log('✅ Python grammar loaded successfully');

        parser.setLanguage(pythonLanguage);
        console.log('🔧 Parser configured with Python language');

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
        vscode.workspace.onDidChangeTextDocument(event => {
            if (event.document === vscode.window.activeTextEditor?.document) {
                analyzeActiveEditor();
            }
        });
        vscode.workspace.onDidChangeConfiguration(event => {
            if (event.affectsConfiguration('energyStateAnalyzer')) {
                analyzeActiveEditor();
            }
        });

        // Clear diagnostics when document is closed
        vscode.workspace.onDidCloseTextDocument(document => {
            if (document.languageId === 'python') {
                diagnosticsCollection.delete(document.uri);
            }
        });

        // Analyze current editor if open
        analyzeActiveEditor();

        console.log('✅ Energy State Analyzer activated successfully!');
        vscode.window.showInformationMessage('Energy State Analyzer: Ready! Open a Python file to see energy analysis.');

    } catch (error) {
        console.error('Failed to activate Energy State Analyzer:', error);
        vscode.window.showErrorMessage(`Energy State Analyzer failed to activate: ${error}`);
    }
}

function createDecorations() {
    // Pastel eye-friendly colors
    const pastelRed = '#ff9999';    // Soft red for high energy
    const pastelYellow = '#ffdd88'; // Soft yellow for medium energy
    const pastelGreen = '#99dd99';  // Soft green for low energy

    highEnergyDecoration = vscode.window.createTextEditorDecorationType({
        // Subtle background highlight that's still hoverable
        backgroundColor: 'rgba(255, 153, 153, 0.1)',
        borderRadius: '2px',
        // Pastel red lightning for high energy
        gutterIconPath: createLightningIcon(pastelRed),
        gutterIconSize: 'contain'
    });

    mediumEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: 'rgba(255, 221, 136, 0.1)',
        borderRadius: '2px',
        // Pastel yellow lightning for medium energy
        gutterIconPath: createLightningIcon(pastelYellow),
        gutterIconSize: 'contain'
    });

    lowEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: 'rgba(153, 221, 153, 0.1)',
        borderRadius: '2px',
        // Pastel green lightning for low energy
        gutterIconPath: createLightningIcon(pastelGreen),
        gutterIconSize: 'contain'
    });

    // Progressive red heat bands for complexity hotspot lines, mildest to
    // most severe. No gutter icon here — the function-level violation above
    // already owns the gutter icon; these bands just paint where within it.
    complexityHeatDecorations = [
        vscode.window.createTextEditorDecorationType({ backgroundColor: 'rgba(255, 90, 90, 0.10)' }),
        vscode.window.createTextEditorDecorationType({ backgroundColor: 'rgba(255, 70, 70, 0.18)' }),
        vscode.window.createTextEditorDecorationType({ backgroundColor: 'rgba(255, 50, 50, 0.28)' }),
        vscode.window.createTextEditorDecorationType({ backgroundColor: 'rgba(255, 30, 30, 0.42)' })
    ];
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

    if (!editor.document.fileName.endsWith('.py')) {
        console.log('⚠️ Not a Python file:', editor.document.fileName);
        // Clear diagnostics for non-Python files
        diagnosticsCollection.clear();
        return;
    }

    console.log('📄 Analyzing Python file:', editor.document.fileName);
    const violations = analyzeDocument(editor.document);
    console.log('🔍 Found', violations.length, 'energy violations');

    // Apply both visual decorations AND problems panel
    applyDecorations(editor, violations);
    updateProblemsPanel(editor.document, violations);
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

function analyzeDocument(document: vscode.TextDocument): EnergyViolation[] {
    const sourceCode = document.getText();

    try {
        const tree = parser.parse(sourceCode);
        const violations = analyzeSource(sourceCode, tree, PYTHON, document.fileName, {
            cyclomatic: getCyclomaticThresholds(),
            cognitive: getCognitiveThresholds()
        });

        // Extract type information (for analysis/future features)
        const typeInfo = extractTypeInformation(tree, document);
        console.log('🔍 Found types:', typeInfo);

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
        // Create better highlight ranges based on violation type
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
// Intensity is normalized per-violation: the single worst line in a function
// is always the darkest band, regardless of how that function compares to
// others in the file.
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

function updateProblemsPanel(document: vscode.TextDocument, violations: EnergyViolation[]) {
    const diagnostics: vscode.Diagnostic[] = violations.map(violation => {
        // Create range for the violation
        const range = new vscode.Range(
            violation.line, violation.column,
            violation.line, violation.column + 10
        );

        // Map energy severity to VSCode diagnostic severity
        let severity: vscode.DiagnosticSeverity;
        switch (violation.severity) {
            case SEVERITY.HIGH:
                severity = vscode.DiagnosticSeverity.Error;
                break;
            case SEVERITY.MEDIUM:
                severity = vscode.DiagnosticSeverity.Warning;
                break;
            case SEVERITY.LOW:
                severity = vscode.DiagnosticSeverity.Information;
                break;
        }

        // Create diagnostic
        const diagnostic = new vscode.Diagnostic(
            range,
            violation.message,
            severity
        );

        // Add metadata
        diagnostic.source = 'Energy State Analyzer';
        diagnostic.code = `energy-${violation.type}`;

        // Add tags for better categorization
        switch (violation.type) {
            case VIOLATION_TYPE.NESTING:
                diagnostic.tags = [vscode.DiagnosticTag.Unnecessary];
                break;
            case VIOLATION_TYPE.COMPLEXITY:
            case VIOLATION_TYPE.COGNITIVE:
                diagnostic.tags = [vscode.DiagnosticTag.Deprecated]; // Using as a visual cue
                break;
        }

        return diagnostic;
    });

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

    // Find return type (after -> token)
    let returnType: string | null = null;
    const arrowIndex = node.children.findIndex((child: any) => child.text === '->');
    if (arrowIndex !== -1 && arrowIndex + 1 < node.children.length) {
        const returnTypeNode = node.children[arrowIndex + 1];
        if (returnTypeNode.type === 'type') {
            returnType = extractTypeString(returnTypeNode);
        }
    }

    const parameters: ParameterTypeInfo[] = [];
    if (parametersNode) {
        for (const child of parametersNode.children) {
            if (child.type === 'typed_parameter') {
                parameters.push(extractParameterTypeInfo(child));
            } else if (child.type === 'default_parameter') {
                parameters.push(extractDefaultParameterTypeInfo(child));
            } else if (child.type === 'identifier') {
                // Untyped parameter
                parameters.push({
                    name: child.text,
                    type: null,
                    hasDefault: false
                });
            }
        }
    }

    const position = document.positionAt(node.startIndex);
    return {
        name: nameNode?.text || 'unknown',
        line: position.line,
        parameters,
        returnType
    };
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

    const baseClasses: string[] = [];
    let isTypedDict = false;

    if (argumentListNode) {
        for (const child of argumentListNode.children) {
            if (child.type === 'identifier') {
                const baseName = child.text;
                baseClasses.push(baseName);
                if (baseName === 'TypedDict') {
                    isTypedDict = true;
                }
            }
        }
    }

    // Extract fields for TypedDict classes
    const fields: VariableTypeInfo[] = [];
    const blockNode = node.children.find((child: any) => child.type === 'block');
    if (blockNode && isTypedDict) {
        for (const child of blockNode.children) {
            if (child.type === 'expression_statement') {
                const assignment = child.children.find((grandchild: any) => grandchild.type === 'assignment');
                if (assignment) {
                    const fieldInfo = extractVariableTypeInfo(assignment, document);
                    if (fieldInfo) {
                        fields.push(fieldInfo);
                    }
                }
            }
        }
    }

    const position = document.positionAt(node.startIndex);
    return {
        name: nameNode?.text || 'unknown',
        line: position.line,
        baseClasses,
        isTypedDict,
        fields
    };
}

function extractImportInfo(node: any, document: vscode.TextDocument): ImportInfo {
    const position = document.positionAt(node.startIndex);
    const items: string[] = [];
    let module = '';

    if (node.type === 'import_statement') {
        // import module1, module2
        for (const child of node.children) {
            if (child.type === 'dotted_name' || child.type === 'identifier') {
                items.push(child.text);
            }
        }
        module = items[0] || '';
    } else if (node.type === 'import_from_statement') {
        // from module import item1, item2
        const fromIndex = node.children.findIndex((child: any) => child.text === 'from');
        const importIndex = node.children.findIndex((child: any) => child.text === 'import');

        if (fromIndex !== -1 && importIndex !== -1) {
            // Get module name (between 'from' and 'import')
            for (let i = fromIndex + 1; i < importIndex; i++) {
                const child = node.children[i];
                if (child.type === 'dotted_name' || child.type === 'identifier') {
                    module = child.text;
                    break;
                }
            }

            // Get imported items (after 'import')
            for (let i = importIndex + 1; i < node.children.length; i++) {
                const child = node.children[i];
                if (child.type === 'identifier') {
                    items.push(child.text);
                }
            }
        }
    }

    return {
        module,
        items,
        line: position.line
    };
}

function extractTypeString(typeNode: any): string {
    if (typeNode.type === 'type') {
        if (typeNode.children.length === 1) {
            const child = typeNode.children[0];
            if (child.type === 'identifier') {
                return child.text;
            } else if (child.type === 'generic_type') {
                return extractGenericTypeString(child);
            }
        }
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