import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { LanguageAdapter } from '../language';
import { isUtilsFileName, looksLikeSingleDomain, looksLikeSingleDomainByNames } from '../namingCohesion';
import { Position, PositionLookup } from '../position';
import { collectTypeSignals, typeCohesionResult, TypeCohesionResult } from '../typeCohesion';

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

// A class defined in the file, along with the methods nested directly (or transitively,
// through non-class nesting like a method's own closures) inside it.
interface ClassInfo {
    name: string | null;
    node: any;
    baseNames: string[];
    methods: any[];
}

// decision: methods are grouped by their nearest enclosing class rather than folded into the
// same flat function list a free-standing function would land in — a class is already a
// cohesion boundary of its own (see checkClassRelatedness below), so its method count isn't
// this detector's function-count-sprawl concern (that would be a separate "god class" check,
// deliberately out of scope here). A method with no enclosing class (every function in a
// functional-style module) still lands in `freeFunctions`, preserving this detector's existing
// behavior for non-OOP files untouched.
function collectFunctionsClassesAndImports(
    tree: any,
    language: LanguageAdapter
): { freeFunctions: any[]; classes: ClassInfo[]; importSources: Set<string>; firstImportNode: any | null } {
    const freeFunctions: any[] = [];
    const classes: ClassInfo[] = [];
    const importSources = new Set<string>();
    let firstImportNode: any | null = null;
    const { nodeTypes, classDefinitionNodeTypes } = language;

    function traverse(node: any, enclosingClass: ClassInfo | null) {
        if (classDefinitionNodeTypes.includes(node.type)) {
            const classInfo: ClassInfo = {
                name: language.getClassName(node),
                node,
                baseNames: language.getBaseClassNames(node),
                methods: []
            };
            classes.push(classInfo);
            for (const child of node.children) {
                traverse(child, classInfo);
            }
            return;
        }

        if (language.isFunctionDefinition(node)) {
            if (enclosingClass) {
                enclosingClass.methods.push(node);
            } else {
                freeFunctions.push(node);
            }
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
            firstImportNode ??= node;
        }

        for (const child of node.children) {
            traverse(child, enclosingClass);
        }
    }

    traverse(tree.rootNode, null);
    return { freeFunctions, classes, importSources, firstImportNode };
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

// decision: a confirmed type signal (typeResult.result is boolean, not 'insufficient-data')
// is authoritative and short-circuits the naming heuristic entirely - both for a confirmed
// shared type (result === true, e.g. an F#-style module of one-verb-per-operation functions
// sharing no name prefix at all) and for confirmed type diversity (result === false), which
// must NOT be overridden by a coincidentally shared name prefix. The naming heuristic only
// runs when typeResult is 'insufficient-data' (too little type coverage to trust).
function isCohesiveByNamingOrType(
    functions: any[],
    thresholds: CoherenceThresholds,
    typeResult: TypeCohesionResult
): boolean {
    if (typeof typeResult.result === 'boolean') {
        return typeResult.result;
    }
    return looksLikeSingleDomain(functions, thresholds.singleDomainNameShare);
}

// decision: anchored on the first function in the file (source order) rather than line 0 —
// there's no single "worst offender" for a whole-file count signal, but pointing at the first
// function at least lands the reader inside the file instead of at a meaningless (0, 0).
function functionCountViolation(functionCount: number, message: string, position: Position): EnergyViolation {
    return {
        line: position.line,
        column: position.column,
        type: VIOLATION_TYPE.COHERENCE,
        severity: functionCount > HIGH_FUNCTION_COUNT_THRESHOLD ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message
    };
}

// Flag files with too many unrelated functions (utils/helpers sprawl)
// decision: lowers the flagging threshold from 12 to 8 functions when the filename itself signals a grab-bag module (util/helper/common) — the name is treated as a proxy for "already known to lack a single responsibility"
// decision: only ever sees free-standing functions, not class methods (see
// collectFunctionsClassesAndImports) — a method's cohesion is judged relative to its own
// class by checkClassRelatedness below, not by this file-wide function count.
function checkFunctionCountSprawl(
    functions: any[],
    fileName: string,
    thresholds: CoherenceThresholds,
    language: LanguageAdapter,
    positions: PositionLookup
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
    const position = positions.toPosition(functions[0].startIndex);

    if (typeResult.result === false) {
        return functionCountViolation(
            functions.length,
            `File coherence warning: ${functions.length} functions in one file spanning ${typeResult.distinctTypes} unrelated types. This is a stronger sprawl signal than function count alone — the functions don't share a common domain type, so moving them into existing cohesive modules (grouped by the type they operate on) is likely to help more than an arbitrary split.`,
            position
        );
    }

    return functionCountViolation(
        functions.length,
        `File coherence warning: ${functions.length} functions in one file. If they belong to distinct domains, prefer moving them into existing cohesive modules; splitting into a new file only helps if it doesn't just relocate the same imports/coupling.`,
        position
    );
}

// Flag files with too many large functions, regardless of total function count - a module with
// 30 small functions is fine, one with 6 sprawling ones isn't.
// decision: anchored on the first large function in source order, not line 0 — it's the most
// directly actionable of the offenders, rather than an arbitrary or averaged position.
function checkLargeFunctionSprawl(
    functions: any[],
    thresholds: CoherenceThresholds,
    positions: PositionLookup
): EnergyViolation | null {
    const largeFunctions = functions.filter((fn) => lineCount(fn) > thresholds.largeFunctionLines);
    if (largeFunctions.length <= thresholds.maxLargeFunctions) {
        return null;
    }

    const position = positions.toPosition(largeFunctions[0].startIndex);
    return {
        line: position.line,
        column: position.column,
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
// far more lines there than in TS/Python). Anchored on the first import statement in the file,
// rather than line 0, so the violation points somewhere a reader can actually look.
function checkImportSprawl(
    importSources: Set<string>,
    firstImportNode: any | null,
    positions: PositionLookup
): EnergyViolation | null {
    if (importSources.size <= IMPORT_COUNT_THRESHOLD) {
        return null;
    }

    const position = firstImportNode ? positions.toPosition(firstImportNode.startIndex) : { line: 0, column: 0 };
    return {
        line: position.line,
        column: position.column,
        type: VIOLATION_TYPE.COHERENCE,
        severity: importSources.size > HIGH_IMPORT_COUNT_THRESHOLD ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message: `Import sprawl: ${importSources.size} distinct modules imported suggest this file does too much. Splitting only helps if the resulting files don't each still need most of these imports.`
    };
}

// decision: a tiny local union-find over the file's classes, not a general graph library -
// the only operation needed is "merge these two classes' families" then "list the resulting
// families", which a parent-pointer array covers in a few lines.
function unionFind(size: number): { union: (a: number, b: number) => void; find: (i: number) => number } {
    const parent = Array.from({ length: size }, (_, i) => i);
    function find(i: number): number {
        while (parent[i] !== i) {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }
        return i;
    }
    function union(a: number, b: number): void {
        const rootA = find(a);
        const rootB = find(b);
        if (rootA !== rootB) {
            parent[rootA] = rootB;
        }
    }
    return { union, find };
}

// Flag a file whose classes split into multiple families with no relationship to each other -
// the class-level counterpart to checkFunctionCountSprawl's "unrelated types" message, but for
// a different shape of sprawl: several small, internally-cohesive classes that don't belong
// together in the same file, rather than many loose functions.
// decision: unlike checkFunctionCountSprawl, this has no minimum class count before it can
// fire - a class is already a much stronger unit of cohesion than a single function (it's a
// whole type, not one operation), so two totally unrelated classes are worth flagging even at
// just 2, not only past some larger threshold.
// decision: three independent signals link two classes into one family, checked in this
// order because each is progressively weaker evidence: (1) direct inheritance - one class's
// base name is another's own name; (2) shared base - two classes both extend/implement the
// same name, even one not defined in this file at all (e.g. a file of exception classes that
// all extend `Exception` but never reference each other); (3) type cross-reference - a
// method's signature (via collectTypeSignals, the same signal checkFunctionCountSprawl's type
// cohesion uses) touches another class defined in the file, as with a token/token-source pair
// where one constructs or returns the other. If the resulting graph still splits into more
// than one group, a naming-affix fallback (shared prefix or suffix across class names, same
// mechanism as looksLikeSingleDomain for functions) gets one last chance to unify the whole
// file before it's flagged - unlike the function-level type-diversity signal, an unconnected
// class graph is an absence of positive evidence, not a positive diversity measurement, so
// it's not treated as authoritative over naming the way checkFunctionCountSprawl's type
// signal is.
function checkClassRelatedness(
    classes: ClassInfo[],
    thresholds: CoherenceThresholds,
    language: LanguageAdapter,
    positions: PositionLookup
): EnergyViolation | null {
    if (classes.length < 2) {
        return null;
    }

    const names = classes.map((c) => c.name);
    const { union, find } = unionFind(classes.length);

    classes.forEach((cls, i) => {
        for (const baseName of cls.baseNames) {
            const baseIndex = names.findIndex((name) => name !== null && name === baseName);
            if (baseIndex !== -1) {
                union(i, baseIndex);
            }
        }
    });

    const indicesByBaseName = new Map<string, number[]>();
    classes.forEach((cls, i) => {
        for (const baseName of cls.baseNames) {
            const group = indicesByBaseName.get(baseName) ?? [];
            group.push(i);
            indicesByBaseName.set(baseName, group);
        }
    });
    for (const group of indicesByBaseName.values()) {
        for (let i = 1; i < group.length; i++) {
            union(group[0], group[i]);
        }
    }

    classes.forEach((cls, i) => {
        for (const method of cls.methods) {
            for (const type of collectTypeSignals(method, language)) {
                const otherIndex = names.findIndex((name) => name !== null && name === type);
                if (otherIndex !== -1 && otherIndex !== i) {
                    union(i, otherIndex);
                }
            }
        }
    });

    const groups = new Map<number, number[]>();
    classes.forEach((_, i) => {
        const root = find(i);
        const group = groups.get(root) ?? [];
        group.push(i);
        groups.set(root, group);
    });

    if (groups.size <= 1) {
        return null;
    }

    const definiteNames = names.filter((name): name is string => name !== null);
    if (
        definiteNames.length === names.length &&
        looksLikeSingleDomainByNames(definiteNames, thresholds.singleDomainNameShare)
    ) {
        return null;
    }

    const groupList = [...groups.values()]
        .map((indices) => indices.map((i) => names[i] ?? '(anonymous)'))
        .sort((a, b) => b.length - a.length);

    const position = positions.toPosition(classes[0].node.startIndex);
    return {
        line: position.line,
        column: position.column,
        type: VIOLATION_TYPE.COHERENCE,
        severity: groupList.length > 2 ? SEVERITY.HIGH : SEVERITY.MEDIUM,
        message: `File coherence warning: ${classes.length} classes in one file split into ${groupList.length} unrelated groups: ${groupList.map((g) => `{${g.join(', ')}}`).join(' vs ')}. These share no inheritance, type relationship, or naming pattern — each group likely belongs in its own file.`
    };
}

// The "Utils/Helpers Sprawl" detector - detects files losing coherence
export function analyzeFileCoherence(
    tree: any,
    fileName: string,
    language: LanguageAdapter,
    positions: PositionLookup,
    thresholds: CoherenceThresholds = DEFAULT_COHERENCE_THRESHOLDS
): EnergyViolation[] {
    const { freeFunctions, classes, importSources, firstImportNode } = collectFunctionsClassesAndImports(
        tree,
        language
    );
    const allFunctions = [...freeFunctions, ...classes.flatMap((c) => c.methods)];

    return [
        checkFunctionCountSprawl(freeFunctions, fileName, thresholds, language, positions),
        checkLargeFunctionSprawl(allFunctions, thresholds, positions),
        checkImportSprawl(importSources, firstImportNode, positions),
        checkClassRelatedness(classes, thresholds, language, positions)
    ].filter((violation): violation is EnergyViolation => violation !== null);
}
