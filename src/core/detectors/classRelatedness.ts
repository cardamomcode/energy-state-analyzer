import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { LanguageAdapter } from '../language';
import { looksLikeSingleDomainByNames } from '../namingCohesion';
import { PositionLookup } from '../position';
import { collectTypeSignals } from '../typeCohesion';

// A class defined in the file, along with the methods nested directly (or transitively,
// through non-class nesting like a method's own closures) inside it.
export interface ClassInfo {
    name: string | null;
    node: any;
    baseNames: string[];
    methods: any[];
}

// decision: a branded alias, not a bare `number` - two adjacent bare-`number` parameters would
// themselves trip this project's own primitive-obsession swap-risk check (see
// primitiveObsession.ts), and a union-find over array positions is exactly that shape.
type ClassIndex = number;

// decision: a tiny local union-find over the file's classes, not a general graph library -
// the only operation needed is "merge these two classes' families" then "list the resulting
// families", which a parent-pointer array covers in a few lines.
function unionFind(size: number): {
    union: (a: ClassIndex, b: ClassIndex) => void;
    find: (i: ClassIndex) => ClassIndex;
} {
    const parent = Array.from({ length: size }, (_, i) => i);
    function find(i: ClassIndex): ClassIndex {
        while (parent[i] !== i) {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }
        return i;
    }
    function union(a: ClassIndex, b: ClassIndex): void {
        const rootA = find(a);
        const rootB = find(b);
        if (rootA !== rootB) {
            parent[rootA] = rootB;
        }
    }
    return { union, find };
}

// Links two classes directly - one's base name is literally the other's own name (e.g. `class
// CancellationToken(Disposable)` where `Disposable` is itself another class in this file).
function linkDirectInheritance(classes: ClassInfo[], names: (string | null)[], union: (a: number, b: number) => void) {
    classes.forEach((cls, i) => {
        for (const baseName of cls.baseNames) {
            const baseIndex = names.findIndex((name) => name !== null && name === baseName);
            if (baseIndex !== -1) {
                union(i, baseIndex);
            }
        }
    });
}

// Links sibling classes that share a base name in common, even one not itself defined in this
// file at all (e.g. a whole file of exception classes that all extend `Exception` but never
// reference each other).
function linkSharedBase(classes: ClassInfo[], union: (a: number, b: number) => void) {
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
}

// Links two classes whenever a method's signature (via collectTypeSignals, the same signal
// checkFunctionCountSprawl's type cohesion uses in coherence.ts) touches another class defined
// in the file, as with a token/token-source pair where one constructs or returns the other.
function linkTypeCrossReference(
    classes: ClassInfo[],
    names: (string | null)[],
    language: LanguageAdapter,
    union: (a: number, b: number) => void
) {
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
}

// Groups the file's classes into families using three independent signals, checked in this
// order because each is progressively weaker evidence: (1) direct inheritance, (2) a shared
// base class, (3) a type cross-reference between method signatures. See
// checkClassRelatedness's doc for why each signal matters and a worked example of each.
function groupClassesIntoFamilies(classes: ClassInfo[], language: LanguageAdapter): number[][] {
    const names = classes.map((c) => c.name);
    const { union, find } = unionFind(classes.length);

    linkDirectInheritance(classes, names, union);
    linkSharedBase(classes, union);
    linkTypeCrossReference(classes, names, language, union);

    const groups = new Map<number, number[]>();
    classes.forEach((_, i) => {
        const root = find(i);
        const group = groups.get(root) ?? [];
        group.push(i);
        groups.set(root, group);
    });
    return [...groups.values()];
}

// Flag a file whose classes split into multiple families with no relationship to each other -
// the class-level counterpart to checkFunctionCountSprawl's "unrelated types" message
// (coherence.ts), but for a different shape of sprawl: several small, internally-cohesive
// classes that don't belong together in the same file, rather than many loose functions.
// decision: unlike checkFunctionCountSprawl, this has no minimum class count before it can
// fire - a class is already a much stronger unit of cohesion than a single function (it's a
// whole type, not one operation), so two totally unrelated classes are worth flagging even at
// just 2, not only past some larger threshold.
// decision: if groupClassesIntoFamilies's three signals still leave more than one family, a
// naming-affix fallback (shared prefix or suffix across class names, same mechanism as
// looksLikeSingleDomain for functions) gets one last chance to unify the whole file before
// it's flagged - unlike the function-level type-diversity signal, an unconnected class graph
// is an absence of positive evidence, not a positive diversity measurement, so it's not
// treated as authoritative over naming the way checkFunctionCountSprawl's type signal is.
// decision: takes the one threshold it needs (singleDomainNameShare) rather than the whole
// CoherenceThresholds object - that type lives in coherence.ts, which itself needs ClassInfo
// and checkClassRelatedness from this file; importing CoherenceThresholds back here would
// make the two files circularly dependent for no reason beyond convenience.
export function checkClassRelatedness(
    classes: ClassInfo[],
    singleDomainNameShare: number,
    language: LanguageAdapter,
    positions: PositionLookup
): EnergyViolation | null {
    if (classes.length < 2) {
        return null;
    }

    const groups = groupClassesIntoFamilies(classes, language);
    if (groups.length <= 1) {
        return null;
    }

    const names = classes.map((c) => c.name);
    const definiteNames = names.filter((name): name is string => name !== null);
    if (definiteNames.length === names.length && looksLikeSingleDomainByNames(definiteNames, singleDomainNameShare)) {
        return null;
    }

    const groupList = groups
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
