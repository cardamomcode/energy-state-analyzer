// Domain: tree-sitter grammar lifecycle. Owns every web-tree-sitter entry point — Parser.init
// (initializeParser) and per-language load + cache (getOrLoadLanguage). Holds no module state; the
// caches live in the composition root and are injected via GrammarContext. Coherence: keep ≤12
// functions, ≤10 imports.
import * as path from 'path';
const { Parser, Language } = require('web-tree-sitter');
import { LanguageAdapter } from './core/language';
import { LANGUAGES } from './languages';

// Initializes the shared tree-sitter Parser once at activation. Kept in this module so every
// web-tree-sitter entry point (Parser.init, Language.load, new Parser) lives behind grammar.ts.
export async function initializeParser(): Promise<void> {
    await Parser.init();
}

export interface LoadedLanguage {
    adapter: LanguageAdapter;
    parser: any;
}

// Context for grammar loading — owned by the activation composition root (src/extension.ts) so
// this module stays free of mutable singletons. The maps are reset per activation there.
export interface GrammarContext {
    extensionPath: string;
    loadedLanguages: Map<string, LoadedLanguage>;
    inFlightLoads: Map<string, Promise<LoadedLanguage>>;
}

// Loads and caches a language's grammar on first use, keyed by vscode languageId.
// decision: caches the in-flight load promise too, not just the settled result — without
// this, a second analyzeActiveEditor call for the same not-yet-loaded language (e.g. from
// a rapid-fire onDidChangeTextDocument) would kick off its own redundant Language.load
export async function getOrLoadLanguage(languageId: string, ctx: GrammarContext): Promise<LoadedLanguage | undefined> {
    const cached = ctx.loadedLanguages.get(languageId);
    if (cached) {
        return cached;
    }

    const adapter = LANGUAGES[languageId];
    if (!adapter) {
        return undefined;
    }

    let pending = ctx.inFlightLoads.get(languageId);
    if (!pending) {
        pending = (async () => {
            const grammarPath = path.join(ctx.extensionPath, adapter.grammarPath);
            console.log(`📁 Loading ${adapter.id} grammar:`, grammarPath);
            const grammar = await Language.load(grammarPath);
            const languageParser = new Parser();
            languageParser.setLanguage(grammar);
            const loaded: LoadedLanguage = { adapter, parser: languageParser };
            ctx.loadedLanguages.set(adapter.id, loaded);
            console.log(`✅ ${adapter.id} grammar loaded successfully`);
            return loaded;
        })();
        ctx.inFlightLoads.set(languageId, pending);
    }
    return pending;
}
