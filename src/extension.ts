import * as vscode from 'vscode';
import * as path from 'path';
const { Parser, Language } = require('web-tree-sitter');

let parser: any;
let pythonLanguage: any;

// Create diagnostics collection at module level
let diagnosticsCollection: vscode.DiagnosticCollection;

// Energy violation types
interface EnergyViolation {
    line: number;
    column: number;
    type: 'nesting' | 'complexity' | 'naming' | 'coherence' | 'magic' | 'parameters';
    severity: 'low' | 'medium' | 'high';
    message: string;
}

// Decoration types for different energy states
let highEnergyDecoration: vscode.TextEditorDecorationType;
let mediumEnergyDecoration: vscode.TextEditorDecorationType;
let lowEnergyDecoration: vscode.TextEditorDecorationType;

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
        const grammarPath = path.join(context.extensionPath, 'grammars', 'tree-sitter-python.wasm');
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

function analyzeDocument(document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const sourceCode = document.getText();

    try {
        const tree = parser.parse(sourceCode);
        violations.push(...analyzeNesting(tree, document));
        violations.push(...analyzeFunctionComplexity(tree, document));
        violations.push(...analyzeFileCoherence(tree, document));
        violations.push(...analyzeMagicValues(tree, document));
        violations.push(...analyzeParameterCount(tree, document));
    } catch (error) {
        console.error('Error analyzing document:', error);
    }

    return violations;
}

function analyzeNesting(tree: any, document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any, depth: number = 0) {
        // Check for excessive nesting in control structures
        if (['if_statement', 'for_statement', 'while_statement', 'with_statement'].includes(node.type)) {
            if (depth > 3) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: 'nesting',
                    severity: depth > 5 ? 'high' : 'medium',
                    message: `Excessive nesting depth: ${depth}. Consider extracting functions.`
                });
            }
            depth++;
        }

        for (const child of node.children) {
            traverse(child, depth);
        }
    }

    traverse(tree.rootNode);
    return violations;
}

function analyzeFunctionComplexity(tree: any, document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (node.type === 'function_definition') {
            const complexity = calculateCyclomaticComplexity(node);
            if (complexity > 10) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: 'complexity',
                    severity: complexity > 15 ? 'high' : 'medium',
                    message: `High cyclomatic complexity: ${complexity}. Consider breaking down this function.`
                });
            }
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return violations;
}

function calculateCyclomaticComplexity(functionNode: any): number {
    let complexity = 1; // Base complexity

    function countDecisionPoints(node: any) {
        const decisionNodes = [
            'if_statement', 'elif_clause', 'while_statement', 'for_statement',
            'except_clause', 'and', 'or', 'conditional_expression'
        ];

        if (decisionNodes.includes(node.type)) {
            complexity++;
        }

        for (const child of node.children) {
            countDecisionPoints(child);
        }
    }

    countDecisionPoints(functionNode);
    return complexity;
}

function applyDecorations(editor: vscode.TextEditor, violations: EnergyViolation[]) {
    const highEnergyRanges: vscode.DecorationOptions[] = [];
    const mediumEnergyRanges: vscode.DecorationOptions[] = [];
    const lowEnergyRanges: vscode.DecorationOptions[] = [];

    for (const violation of violations) {
        // Create better highlight ranges based on violation type
        let range: vscode.Range;
        const line = editor.document.lineAt(violation.line);
        
        if (violation.type === 'coherence') {
            // Highlight entire first line for file-level issues
            range = new vscode.Range(violation.line, 0, violation.line, line.text.length);
        } else if (violation.type === 'nesting' || violation.type === 'complexity') {
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
            case 'high':
                highEnergyRanges.push(decoration);
                break;
            case 'medium':
                mediumEnergyRanges.push(decoration);
                break;
            case 'low':
                lowEnergyRanges.push(decoration);
                break;
        }
    }

    editor.setDecorations(highEnergyDecoration, highEnergyRanges);
    editor.setDecorations(mediumEnergyDecoration, mediumEnergyRanges);
    editor.setDecorations(lowEnergyDecoration, lowEnergyRanges);
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
            case 'high':
                severity = vscode.DiagnosticSeverity.Error;
                break;
            case 'medium':
                severity = vscode.DiagnosticSeverity.Warning;
                break;
            case 'low':
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
            case 'nesting':
                diagnostic.tags = [vscode.DiagnosticTag.Unnecessary];
                break;
            case 'complexity':
                diagnostic.tags = [vscode.DiagnosticTag.Deprecated]; // Using as a visual cue
                break;
        }

        return diagnostic;
    });

    // Update the Problems panel
    diagnosticsCollection.set(document.uri, diagnostics);
}

// The "Utils/Helpers Sprawl" detector - detects files losing coherence
function analyzeFileCoherence(tree: any, document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    const functions: any[] = [];
    const imports: string[] = [];
    
    // Collect all function definitions and imports
    function traverse(node: any) {
        if (node.type === 'function_definition') {
            functions.push(node);
        } else if (node.type === 'import_statement' || node.type === 'import_from_statement') {
            imports.push(node.text || '');
        }
        
        for (const child of node.children) {
            traverse(child);
        }
    }
    
    traverse(tree.rootNode);
    
    // Flag files with too many unrelated functions (utils/helpers sprawl)
    if (functions.length > 8) {
        const fileName = document.fileName.split('/').pop() || '';
        const isUtilsFile = fileName.includes('util') || fileName.includes('helper') || fileName.includes('common');
        
        if (isUtilsFile || functions.length > 12) {
            violations.push({
                line: 0,
                column: 0,
                type: 'coherence',
                severity: functions.length > 15 ? 'high' : 'medium',
                message: `File coherence warning: ${functions.length} functions in one file. Consider splitting by domain.`
            });
        }
    }
    
    // Flag excessive imports (another sign of incoherence)
    if (imports.length > 10) {
        violations.push({
            line: 0,
            column: 0,
            type: 'coherence',
            severity: imports.length > 15 ? 'high' : 'medium',
            message: `Import sprawl: ${imports.length} imports suggest this file does too much.`
        });
    }
    
    return violations;
}

// The "Magic Numbers/Strings" detector
function analyzeMagicValues(tree: any, document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    
    function traverse(node: any) {
        // Flag suspicious numeric literals
        if (node.type === 'integer' || node.type === 'float') {
            const value = parseInt(node.text) || parseFloat(node.text);
            const isSignificant = value > 1 && value !== 100 && value !== 1000; // Allow common values
            
            if (isSignificant && !isInConstantContext(node)) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: 'magic',
                    severity: 'low',
                    message: `Magic number: ${node.text}. Consider extracting to a named constant.`
                });
            }
        }
        
        // Flag suspicious string literals (potential config/messages)
        if (node.type === 'string' && node.text.length > 15) {
            const content = node.text.slice(1, -1); // Remove quotes
            const looksLikeMessage = content.includes(' ') && (content.includes('error') || content.includes('invalid') || content.includes('not found'));
            
            if (looksLikeMessage) {
                const position = document.positionAt(node.startIndex);
                violations.push({
                    line: position.line,
                    column: position.character,
                    type: 'magic',
                    severity: 'low',
                    message: `Magic string: Consider extracting error messages to constants.`
                });
            }
        }
        
        for (const child of node.children) {
            traverse(child);
        }
    }
    
    function isInConstantContext(node: any): boolean {
        // Simple heuristic: check if parent is an assignment at module level
        let parent = node.parent;
        while (parent) {
            if (parent.type === 'assignment' && parent.parent?.type === 'module') {
                return true;
            }
            parent = parent.parent;
        }
        return false;
    }
    
    traverse(tree.rootNode);
    return violations;
}

// The "Parameter Explosion" detector
function analyzeParameterCount(tree: any, document: vscode.TextDocument): EnergyViolation[] {
    const violations: EnergyViolation[] = [];
    
    function traverse(node: any) {
        if (node.type === 'function_definition') {
            const params = node.children.find((child: any) => child.type === 'parameters');
            if (params) {
                const paramCount = params.children.filter((child: any) => 
                    child.type === 'identifier' || child.type === 'default_parameter'
                ).length;
                
                if (paramCount > 5) {
                    const position = document.positionAt(node.startIndex);
                    violations.push({
                        line: position.line,
                        column: position.character,
                        type: 'parameters',
                        severity: paramCount > 8 ? 'high' : 'medium',
                        message: `Parameter explosion: ${paramCount} parameters. Consider using objects or builder pattern.`
                    });
                }
            }
        }
        
        for (const child of node.children) {
            traverse(child);
        }
    }
    
    traverse(tree.rootNode);
    return violations;
}

export function deactivate() {
    // Clean up decorations AND diagnostics
    highEnergyDecoration?.dispose();
    mediumEnergyDecoration?.dispose();
    lowEnergyDecoration?.dispose();
    diagnosticsCollection?.dispose();
}