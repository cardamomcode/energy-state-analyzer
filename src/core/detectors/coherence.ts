import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { LanguageAdapter } from '../language';
import { typeCohesionResult, TypeCohesionResult } from '../typeCohesion';

export interface CoherenceThresholds {
    // decision: gates file-coherence sprawl detection on large-function count, not raw function count — languages like F# idiomatically have many small functions per module, so what matters is functions large enough to carry real complexity
    largeFunctionLines: number;
    // Number of large functions (per largeFunctionLines) a file can contain
    // before it's flagged.
    maxLargeFunctions: number;
    // Share (0-1) of a file's functions that must share a leading name word (e.g. all
    // `extractX`/`extractY`) for the file to be treated as one coherent domain broken into many
    // small steps, rather than a grab-bag of unrelated helpers, and skip the raw function-count
    // sprawl check below. Only consulted when there isn't enough type-annotation coverage to
    // trust maxTypeDiversityRatio instead - see checkFunctionCountSprawl.
    singleDomainNameShare: number;
    // Maximum allowed ratio (0-1) of distinct base types to typed functions (those with >=1
    // typed parameter or return-type annotation) for the file to be treated as one
    // type-cohesive module, skipping the function-count sprawl check outright. A stronger
    // signal than singleDomainNameShare when available, since it isn't vulnerable to
    // name-prefix coincidence: an F#-style module exposing one verb per operation (map/
    // filter/fold/zip/scan/...) shares no name prefix at all, but reuses a small type
    // vocabulary throughout. Measures reuse (few distinct types across many functions) rather
    // than one type dominating, since a real cohesive module often legitimately spans a
    // *family* of related types (e.g. a Seq module touching Iterable, Seq, and Iterator) -
    // requiring one single type to reach a majority share was tried first and rejected after
    // it false-negatived on exactly that case. Only ever evaluated once a file already
    // crosses the existing function-count thresholds (8 utils-named, 12 generic) below - a
    // separate, lower threshold letting this signal flag files earlier was also tried and
    // rejected, after it false-positived on this project's own coherence.ts (see
    // checkFunctionCountSprawl's doc): type diversity alone isn't reliable enough below ~12
    // functions to tell a legitimately-typed small module apart from a real grab-bag.
    maxTypeDiversityRatio: number;
    // Minimum share (0-1) of a file's functions that must carry at least one typed parameter
    // or return-type annotation before maxTypeDiversityRatio is trusted at all. Below this,
    // the file is treated as having insufficient type data and the detector falls back to
    // singleDomainNameShare instead - avoids false confidence on largely-untyped files, where
    // a handful of coincidentally same-typed functions could otherwise fake a low ratio.
    minTypedCoverage: number;
}

export const DEFAULT_COHERENCE_THRESHOLDS: CoherenceThresholds = {
    largeFunctionLines: 20,
    maxLargeFunctions: 5,
    singleDomainNameShare: 0.7,
    maxTypeDiversityRatio: 0.4,
    minTypedCoverage: 0.5
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

function collectFunctionsAndImports(
    tree: any,
    language: LanguageAdapter
): { functions: any[]; importSources: Set<string> } {
    const functions: any[] = [];
    const importSources = new Set<string>();
    const { nodeTypes } = language;

    function traverse(node: any) {
        if (language.isFunctionDefinition(node)) {
            functions.push(node);
        } else if (
            node.isNamed &&
            (node.type === nodeTypes.importStatement || node.type === nodeTypes.importFromStatement)
        ) {
            // decision: requires isNamed, not just a type match — Kotlin's import rule is
            // literally named `import`, which collides with the anonymous `import` keyword
            // token that is itself a child of every import node (node.type for an anonymous
            // node is its literal text). Without this guard, every Kotlin import is counted
            // twice: once for the named node, once for its own leading keyword token.
            importSources.add(language.importSource(node) || node.text || '');
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return { functions, importSources };
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

function isUtilsFileName(fileName: string): boolean {
    const baseName = fileName.split('/').pop() || '';
    return baseName.includes('util') || baseName.includes('helper') || baseName.includes('common');
}

// decision: typeResult.result === true (measured, confirmed shared type - e.g. an F#-style
// module of one-verb-per-operation functions sharing no name prefix at all) is a stronger
// signal than looksLikeSingleDomain and is checked first; the naming heuristic only runs
// when typeResult is 'insufficient-data' (too little type coverage to trust).
function isCohesiveByNamingOrType(
    functions: any[],
    thresholds: CoherenceThresholds,
    typeResult: TypeCohesionResult
): boolean {
    return typeResult.result === true || looksLikeSingleDomain(functions, thresholds.singleDomainNameShare);
}

function functionCountViolation(functionCount: number, message: string): EnergyViolation {
    return {
        line: 0,
        column: 0,
        type: VIOLATION_TYPE.COHERENCE,
        severity: functionCount > HIGH_FUNCTION_COUNT_THRESHOLD ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message
    };
}

// Flag files with too many unrelated functions (utils/helpers sprawl)
// decision: lowers the flagging threshold from 12 to 8 functions when the filename itself signals a grab-bag module (util/helper/common) — the name is treated as a proxy for "already known to lack a single responsibility"
function checkFunctionCountSprawl(
    functions: any[],
    fileName: string,
    thresholds: CoherenceThresholds,
    language: LanguageAdapter
): EnergyViolation | null {
    if (functions.length <= UTILS_FILE_FUNCTION_THRESHOLD) {
        return null;
    }

    const isUtilsFile = isUtilsFileName(fileName);
    const typeResult = typeCohesionResult(functions, language, {
        maxDiversityRatio: thresholds.maxTypeDiversityRatio,
        minCoverage: thresholds.minTypedCoverage
    });

    // decision: an explicit utils/helper/common filename overrides either cohesion signal
    // (naming or type) — a module that already admits to being a grab-bag in its own name
    // doesn't get to argue its way out via consistent prefixes or a shared type.
    const singleDomain = !isUtilsFile && isCohesiveByNamingOrType(functions, thresholds, typeResult);

    if (!isUtilsFile && (functions.length <= GENERIC_FUNCTION_COUNT_THRESHOLD || singleDomain)) {
        return null;
    }

    // decision: the type signal is evaluated at the SAME function-count thresholds as the
    // naming/utils-filename path above (8 for utils-named files, 12 generic), not a lower,
    // earlier-firing one — a lower threshold was tried and rejected after dogfooding surfaced
    // a real false positive on this project's own coherence.ts (9 small, purpose-cohesive
    // helper functions with a few different supporting types - not an entropy dump), showing
    // the type-diversity signal isn't reliable enough below ~12 functions to tell a
    // legitimately-typed small module apart from a real grab-bag. Once a file is already
    // going to be flagged at the existing thresholds, though, a confidently-diverse type
    // result is authoritative over naming (see CoherenceThresholds.maxTypeDiversityRatio's
    // doc) and gets the stronger, more specific message below instead of the generic one.
    if (typeResult.result === false) {
        return functionCountViolation(
            functions.length,
            `File coherence warning: ${functions.length} functions in one file spanning ${typeResult.distinctTypes} unrelated types. This is a stronger sprawl signal than function count alone — the functions don't share a common domain type, so moving them into existing cohesive modules (grouped by the type they operate on) is likely to help more than an arbitrary split.`
        );
    }

    return functionCountViolation(
        functions.length,
        `File coherence warning: ${functions.length} functions in one file. If they belong to distinct domains, prefer moving them into existing cohesive modules; splitting into a new file only helps if it doesn't just relocate the same imports/coupling.`
    );
}

// Flag files with too many large functions, regardless of total function count - a module with
// 30 small functions is fine, one with 6 sprawling ones isn't.
function checkLargeFunctionSprawl(functions: any[], thresholds: CoherenceThresholds): EnergyViolation | null {
    const largeFunctions = functions.filter((fn) => lineCount(fn) > thresholds.largeFunctionLines);
    if (largeFunctions.length <= thresholds.maxLargeFunctions) {
        return null;
    }

    return {
        line: 0,
        column: 0,
        type: VIOLATION_TYPE.COHERENCE,
        severity:
            largeFunctions.length > thresholds.maxLargeFunctions * LARGE_FUNCTION_SEVERITY_MULTIPLIER
                ? SEVERITY.HIGH
                : SEVERITY.MEDIUM,
        message: `${largeFunctions.length} functions exceed ${thresholds.largeFunctionLines} lines. Large functions carry more complexity than function count alone suggests.`
    };
}

// Flag excessive imports (another sign of incoherence)
// decision: counts distinct import *sources* (modules/packages), not raw import lines/symbols —
// see LanguageAdapter.importSource's doc for why raw-line counting isn't comparable across
// languages (Kotlin has no brace-grouped import syntax, so the same set of dependencies costs
// far more lines there than in TS/Python).
function checkImportSprawl(importSources: Set<string>): EnergyViolation | null {
    if (importSources.size <= IMPORT_COUNT_THRESHOLD) {
        return null;
    }

    return {
        line: 0,
        column: 0,
        type: VIOLATION_TYPE.COHERENCE,
        severity: importSources.size > HIGH_IMPORT_COUNT_THRESHOLD ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message: `Import sprawl: ${importSources.size} distinct modules imported suggest this file does too much. Splitting only helps if the resulting files don't each still need most of these imports.`
    };
}

// The "Utils/Helpers Sprawl" detector - detects files losing coherence
export function analyzeFileCoherence(
    tree: any,
    fileName: string,
    language: LanguageAdapter,
    thresholds: CoherenceThresholds = DEFAULT_COHERENCE_THRESHOLDS
): EnergyViolation[] {
    const { functions, importSources } = collectFunctionsAndImports(tree, language);

    return [
        checkFunctionCountSprawl(functions, fileName, thresholds, language),
        checkLargeFunctionSprawl(functions, thresholds),
        checkImportSprawl(importSources)
    ].filter((violation): violation is EnergyViolation => violation !== null);
}
