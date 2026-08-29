// Composition root for the Energy State Analyzer VS Code extension. This file OWNS activation
// wiring and cross-module coordination only — it creates collaborators, threads shared state (grammar
// caches, decoration set, diagnostics collection) into them, and wires event handlers. It contains no
// detector, presentation, or analysis logic of its own; if you find yourself adding business logic
// here, extract it into a domain module (src/decorations.ts, src/diagnostics.ts, src/grammar.ts,
// src/analysis.ts). Coherence: keep ≤12 functions, ≤10 imports.
import * as vscode from 'vscode';

import { applyDecorations, createDecorations, disposeDecorations, DecorationSet } from './decorations';
import { updateProblemsPanel } from './diagnostics';
import { getOrLoadLanguage, initializeParser, LoadedLanguage } from './grammar';
import { analyzeDocument, isDocumentIgnored } from './analysis';
import { LANGUAGES } from './languages';

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

// The decoration types created once per activation and threaded through the presentation
// functions in ./decorations — this composition root owns them so that module holds no state.
// Assigned synchronously in activate before any event handler can run, mirroring the original
// non-optional module-level decoration bindings.
let decorations: DecorationSet;

export async function activate(context: vscode.ExtensionContext) {
    console.log('🚀 Activating Energy State Analyzer...');

    try {
        // Initialize Parser
        console.log('🔧 Initializing Parser...');
        await initializeParser();
        console.log('✅ Parser initialized');

        loadedLanguages = new Map();
        inFlightLoads = new Map();
        extensionPath = context.extensionPath;

        // Create decoration types
        decorations = createDecorations();
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
                disposeDecorations(decorations);
                decorations = createDecorations();
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

async function analyzeActiveEditor() {
    const editor = vscode.window.activeTextEditor;
    console.log('🔍 Analyzing active editor...');

    if (!editor) {
        console.log('❌ No active editor found');
        return;
    }

    if (isDocumentIgnored(editor.document)) {
        console.log('🚫 Ignored by .esaignore:', editor.document.fileName);
        applyDecorations(editor, decorations, []);
        diagnosticsCollection.delete(editor.document.uri);
        return;
    }

    const loaded = await getOrLoadLanguage(editor.document.languageId, {
        extensionPath,
        loadedLanguages,
        inFlightLoads
    });
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
    applyDecorations(editor, decorations, violations);
    updateProblemsPanel(diagnosticsCollection, editor.document, violations);
}

export function deactivate() {
    // Clean up decorations AND diagnostics
    if (decorations) {
        disposeDecorations(decorations);
    }
    diagnosticsCollection?.dispose();
}
