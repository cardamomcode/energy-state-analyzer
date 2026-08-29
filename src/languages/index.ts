import { LanguageAdapter } from '../core/language';
import { PYTHON } from './python';
import { FSHARP } from './fsharp';
import { TYPESCRIPT } from './typescript';
import { KOTLIN } from './kotlin';

// Re-export the individual adapters so callers can import a specific language (e.g. PYTHON)
// straight from this barrel without reaching into ./languages/python — keeps extension.ts and
// analysis.ts at one fewer import source after the domain split (see #30).
export { PYTHON };

// decision: keys LANGUAGES by vscode languageId rather than file extension — the extension host already resolves languageId, and resolveLanguageForFile below provides the file-extension equivalent for the CLI
export const LANGUAGES: Record<string, LanguageAdapter> = {
    python: PYTHON,
    fsharp: FSHARP,
    typescript: TYPESCRIPT,
    kotlin: KOTLIN
};

const EXTENSION_TO_LANGUAGE_ID: Record<string, string> = {
    '.py': 'python',
    '.fs': 'fsharp',
    '.fsx': 'fsharp',
    '.fsi': 'fsharp',
    '.ts': 'typescript',
    '.kt': 'kotlin',
    '.kts': 'kotlin'
};

export function resolveLanguageForFile(fileName: string): LanguageAdapter | undefined {
    const dotIndex = fileName.lastIndexOf('.');
    if (dotIndex === -1) {
        return undefined;
    }
    const extension = fileName.slice(dotIndex).toLowerCase();
    const languageId = EXTENSION_TO_LANGUAGE_ID[extension];
    return languageId ? LANGUAGES[languageId] : undefined;
}
