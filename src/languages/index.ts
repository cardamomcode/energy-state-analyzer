import { LanguageAdapter } from '../core/language';
import { PYTHON } from './python';
import { FSHARP } from './fsharp';
import { TYPESCRIPT } from './typescript';

// Keyed by vscode languageId, which is also what we use to pick an adapter
// for a given file (see resolveLanguageForFile for the CLI's file-extension
// equivalent).
export const LANGUAGES: Record<string, LanguageAdapter> = {
    python: PYTHON,
    fsharp: FSHARP,
    typescript: TYPESCRIPT
};

const EXTENSION_TO_LANGUAGE_ID: Record<string, string> = {
    '.py': 'python',
    '.fs': 'fsharp',
    '.fsx': 'fsharp',
    '.fsi': 'fsharp',
    '.ts': 'typescript'
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
