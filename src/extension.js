"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const Parser = require('web-tree-sitter');
const vscode = __importStar(require("vscode"));
const path = __importStar(require("path"));
let parser;
let pythonLanguage;
// Decoration types for different energy states
let highEnergyDecoration;
let mediumEnergyDecoration;
let lowEnergyDecoration;
async function activate(context) {
    console.log('Activating Energy State Analyzer...');
    try {
        // Initialize tree-sitter
        await Parser.init();
        // Load Python grammar
        const grammarPath = path.join(context.extensionPath, 'grammars', 'tree-sitter-python.wasm');
        pythonLanguage = await Parser.Language.load(grammarPath);
        parser = new Parser();
        parser.setLanguage(pythonLanguage);
        // Create decoration types
        createDecorations();
        // Register event listeners
        vscode.window.onDidChangeActiveTextEditor(analyzeActiveEditor);
        vscode.workspace.onDidChangeTextDocument(event => {
            if (event.document === vscode.window.activeTextEditor?.document) {
                analyzeActiveEditor();
            }
        });
        // Analyze current editor if open
        analyzeActiveEditor();
        console.log('Energy State Analyzer activated successfully!');
    }
    catch (error) {
        console.error('Failed to activate Energy State Analyzer:', error);
        vscode.window.showErrorMessage(`Energy State Analyzer failed to activate: ${error}`);
    }
}
function createDecorations() {
    highEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: 'rgba(255, 0, 0, 0.1)',
        border: '1px solid rgba(255, 0, 0, 0.3)',
        after: {
            contentText: ' ⚡ High Energy',
            color: 'rgba(255, 0, 0, 0.8)',
            fontStyle: 'italic'
        }
    });
    mediumEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: 'rgba(255, 165, 0, 0.1)',
        border: '1px solid rgba(255, 165, 0, 0.3)',
        after: {
            contentText: ' ⚠️ Medium Energy',
            color: 'rgba(255, 165, 0, 0.8)',
            fontStyle: 'italic'
        }
    });
    lowEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: 'rgba(255, 255, 0, 0.1)',
        border: '1px solid rgba(255, 255, 0, 0.3)',
        after: {
            contentText: ' 💡 Attention',
            color: 'rgba(255, 255, 0, 0.8)',
            fontStyle: 'italic'
        }
    });
}
function analyzeActiveEditor() {
    const editor = vscode.window.activeTextEditor;
    if (!editor || !editor.document.fileName.endsWith('.py')) {
        return;
    }
    const violations = analyzeDocument(editor.document);
    applyDecorations(editor, violations);
}
function analyzeDocument(document) {
    const violations = [];
    const sourceCode = document.getText();
    try {
        const tree = parser.parse(sourceCode);
        violations.push(...analyzeNesting(tree, document));
        violations.push(...analyzeFunctionComplexity(tree, document));
    }
    catch (error) {
        console.error('Error analyzing document:', error);
    }
    return violations;
}
function analyzeNesting(tree, document) {
    const violations = [];
    function traverse(node, depth = 0) {
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
function analyzeFunctionComplexity(tree, document) {
    const violations = [];
    function traverse(node) {
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
function calculateCyclomaticComplexity(functionNode) {
    let complexity = 1; // Base complexity
    function countDecisionPoints(node) {
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
function applyDecorations(editor, violations) {
    const highEnergyRanges = [];
    const mediumEnergyRanges = [];
    const lowEnergyRanges = [];
    for (const violation of violations) {
        const range = new vscode.Range(violation.line, violation.column, violation.line, violation.column + 10 // Highlight a small range
        );
        const decoration = {
            range,
            hoverMessage: violation.message
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
function deactivate() {
    // Clean up decorations
    highEnergyDecoration?.dispose();
    mediumEnergyDecoration?.dispose();
    lowEnergyDecoration?.dispose();
}
//# sourceMappingURL=extension.js.map