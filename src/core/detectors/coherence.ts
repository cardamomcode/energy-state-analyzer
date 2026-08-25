import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { LanguageAdapter } from '../language';

export interface CoherenceThresholds {
    // decision: gates file-coherence sprawl detection on large-function count, not raw function count — languages like F# idiomatically have many small functions per module, so what matters is functions large enough to carry real complexity
    largeFunctionLines: number;
    // Number of large functions (per largeFunctionLines) a file can contain
    // before it's flagged.
    maxLargeFunctions: number;
    // Share (0-1) of a file's functions that must share a leading name word (e.g. all
    // `extractX`/`extractY`) for the file to be treated as one coherent domain broken into many
    // small steps, rather than a grab-bag of unrelated helpers, and skip the raw function-count
    // sprawl check below.
    singleDomainNameShare: number;
}

export const DEFAULT_COHERENCE_THRESHOLDS: CoherenceThresholds = {
    largeFunctionLines: 20,
    maxLargeFunctions: 5,
    singleDomainNameShare: 0.7
};

function lineCount(node: any): number {
    return node.endPosition.row - node.startPosition.row + 1;
}

// decision: a raw function name is a weak signal on its own, but a *dominant leading word*
// shared across most of a file's functions (extractFoo/extractBar/extractBaz) is a cheap,
// AST-only proxy for "this file is one coherent domain factored into many small steps" —
// exactly the case the function-count sprawl check below would otherwise misflag, since it
// can't tell that apart from an actual grab-bag of unrelated helpers by count alone
function leadingNameWord(node: any): string | null {
    const nameNode = node.children?.find((c: any) => c.type === 'identifier');
    if (!nameNode?.text) {
        return null;
    }
    const match = /^[a-z]+/.exec(nameNode.text);
    return match ? match[0] : nameNode.text.toLowerCase();
}

function looksLikeSingleDomain(functions: any[], minShare: number): boolean {
    const leadingWords = functions.map(leadingNameWord).filter((word): word is string => word !== null);
    if (leadingWords.length === 0) {
        return false;
    }

    const wordCounts = new Map<string, number>();
    for (const word of leadingWords) {
        wordCounts.set(word, (wordCounts.get(word) ?? 0) + 1);
    }

    const dominantWordCount = Math.max(...wordCounts.values());
    return dominantWordCount / leadingWords.length >= minShare;
}

function collectFunctionsAndImports(tree: any, language: LanguageAdapter): { functions: any[]; imports: string[] } {
    const functions: any[] = [];
    const imports: string[] = [];
    const { nodeTypes } = language;

    function traverse(node: any) {
        if (language.isFunctionDefinition(node)) {
            functions.push(node);
        } else if (node.type === nodeTypes.importStatement || node.type === nodeTypes.importFromStatement) {
            imports.push(node.text || '');
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return { functions, imports };
}

// decision: the raw function-count trigger (8/12) and its severity escalation (15) are
// deliberately not part of CoherenceThresholds like largeFunctionLines/maxLargeFunctions are —
// they're secondary heuristics tuned around the utils-file naming proxy, not a threshold users
// are expected to retune independently
const UTILS_FILE_FUNCTION_THRESHOLD = 8;
const GENERIC_FUNCTION_COUNT_THRESHOLD = 12;
const HIGH_FUNCTION_COUNT_THRESHOLD = 15;
const LARGE_FUNCTION_SEVERITY_MULTIPLIER = 1.5;
const IMPORT_COUNT_THRESHOLD = 10;
const HIGH_IMPORT_COUNT_THRESHOLD = 15;

// Flag files with too many unrelated functions (utils/helpers sprawl)
// decision: lowers the flagging threshold from 12 to 8 functions when the filename itself signals a grab-bag module (util/helper/common) — the name is treated as a proxy for "already known to lack a single responsibility"
function checkFunctionCountSprawl(functions: any[], fileName: string, thresholds: CoherenceThresholds): EnergyViolation | null {
    if (functions.length <= UTILS_FILE_FUNCTION_THRESHOLD) {
        return null;
    }

    const baseName = fileName.split('/').pop() || '';
    const isUtilsFile = baseName.includes('util') || baseName.includes('helper') || baseName.includes('common');
    // decision: an explicit utils/helper/common filename overrides the naming-cohesion signal — a module that already admits to being a grab-bag in its own name doesn't get to argue its way out via consistent prefixes
    const singleDomain = !isUtilsFile && looksLikeSingleDomain(functions, thresholds.singleDomainNameShare);

    if (!isUtilsFile && (functions.length <= GENERIC_FUNCTION_COUNT_THRESHOLD || singleDomain)) {
        return null;
    }

    return {
        line: 0,
        column: 0,
        type: VIOLATION_TYPE.COHERENCE,
        severity: functions.length > HIGH_FUNCTION_COUNT_THRESHOLD ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message: `File coherence warning: ${functions.length} functions in one file. Consider splitting by domain.`
    };
}

// Flag files with too many large functions, regardless of total function count - a module with
// 30 small functions is fine, one with 6 sprawling ones isn't.
function checkLargeFunctionSprawl(functions: any[], thresholds: CoherenceThresholds): EnergyViolation | null {
    const largeFunctions = functions.filter(fn => lineCount(fn) > thresholds.largeFunctionLines);
    if (largeFunctions.length <= thresholds.maxLargeFunctions) {
        return null;
    }

    return {
        line: 0,
        column: 0,
        type: VIOLATION_TYPE.COHERENCE,
        severity: largeFunctions.length > thresholds.maxLargeFunctions * LARGE_FUNCTION_SEVERITY_MULTIPLIER ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message: `${largeFunctions.length} functions exceed ${thresholds.largeFunctionLines} lines. Large functions carry more complexity than function count alone suggests.`
    };
}

// Flag excessive imports (another sign of incoherence)
function checkImportSprawl(imports: string[]): EnergyViolation | null {
    if (imports.length <= IMPORT_COUNT_THRESHOLD) {
        return null;
    }

    return {
        line: 0,
        column: 0,
        type: VIOLATION_TYPE.COHERENCE,
        severity: imports.length > HIGH_IMPORT_COUNT_THRESHOLD ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message: `Import sprawl: ${imports.length} imports suggest this file does too much.`
    };
}

// The "Utils/Helpers Sprawl" detector - detects files losing coherence
export function analyzeFileCoherence(
    tree: any,
    fileName: string,
    language: LanguageAdapter,
    thresholds: CoherenceThresholds = DEFAULT_COHERENCE_THRESHOLDS
): EnergyViolation[] {
    const { functions, imports } = collectFunctionsAndImports(tree, language);

    return [
        checkFunctionCountSprawl(functions, fileName, thresholds),
        checkLargeFunctionSprawl(functions, thresholds),
        checkImportSprawl(imports)
    ].filter((violation): violation is EnergyViolation => violation !== null);
}
