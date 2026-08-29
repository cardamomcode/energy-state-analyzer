// Domain: document analysis + ignore gating. Owns turning a parsed buffer into violations
// (analyzeDocument) and deciding whether a document is ignored (.esaignore / includeFixtures —
// isDocumentIgnored). Pure orchestration over core/* analyzers; no presentation state. Coherence:
// keep ≤12 functions, ≤10 imports.
import * as vscode from 'vscode';

import { EnergyViolation } from './types';
import { analyzeSource } from './core/analyze';
import { createPositionLookup } from './core/position';
import { extractTypeInformation } from './core/pythonTypeInfo';
import { readAnalyzeThresholds } from './config';
import { isIgnored, loadIgnorePatterns } from './core/esaignore';
import { PYTHON } from './languages';
import { LoadedLanguage } from './grammar';

// A document with no containing workspace folder (e.g. a file opened standalone) has
// nowhere to look for a `.esaignore`, so it's never treated as ignored.
//
// `includeFixtures` is an editor-only override for visually spot-checking detector
// fixtures (deliberately bad code under .esaignore, e.g. src/test/fixtures) without
// touching .esaignore itself, which the CLI/CI scan (src/cliModes.ts) always honors.
const INCLUDE_FIXTURES_DEFAULT = false;

export function isDocumentIgnored(document: vscode.TextDocument): boolean {
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

export function analyzeDocument(document: vscode.TextDocument, loaded: LoadedLanguage): EnergyViolation[] {
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
