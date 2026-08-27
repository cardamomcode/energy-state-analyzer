import { LanguageAdapter } from './language';
import { findParametersNode } from './detectors/parameterCount';

// decision: excluded outright rather than counted as a "domain type" - each of these names
// the *shape* of a value (a callback, an absent/untyped result), not what a function
// operates on. Confirmed necessary empirically: running this detector against a real
// F#-style Python module (expression/collections/seq.py, ~97 functions almost all touching
// Iterable/Seq) initially still misfired, because ~45% of its functions take a Callable
// callback parameter alongside their real domain type - left uncounted, Callable would have
// out-voted the actual dominant type (Iterable) for "most common base type in the file".
const NON_DOMAIN_BASE_TYPES = new Set(['Callable', 'Function', 'Any', 'None', 'Unit', 'void']);

// PEP-484/TS/Kotlin generic type-parameter naming conventions (bare `T`/`U`/`K`/`V`, or
// Python's leading-underscore `_TSource`/`_TState`/`_T1` convention) - these name "the same
// generic slot", not a concrete type, and are excluded for the same reason as
// NON_DOMAIN_BASE_TYPES above: a function's own unbound type parameter says nothing about
// its domain (every generic function in the file has one, regardless of what it operates on).
function isTypeParameterName(name: string): boolean {
    return /^[A-Z]$/.test(name) || /^_?T([A-Z]\w*|\d*)$/.test(name);
}

// Strips generic type arguments from a raw type-text blob (as returned by
// extractTypedParameter/extractReturnType) down to a comparable base type name, e.g.
// "Iterable<T>" -> "Iterable", "Iterable[_TSource]" -> "Iterable". Returns null for shapes
// that aren't a plain (possibly generic) named type - function types ("(x: T) => U"), tuple
// types ("int * string") - since those don't represent "this function operates on domain
// type X" and guessing would produce noise rather than signal. Also returns null for
// NON_DOMAIN_BASE_TYPES and type-parameter-shaped names - see their docs above.
//
// known gap: wrapper generics (Optional[str], Dict[str, int]) normalize to their wrapper
// base (Optional, Dict), not the wrapped domain type - same for F#'s postfix `int option`
// syntax, which has no bracket at all and is rejected outright by the identifier check below.
// Unwrapping common wrappers per language would reopen the per-language special-casing this
// shared, text-based helper is designed to avoid; left as a documented v1 limitation.
export function baseTypeName(typeText: string, brackets: { open: string; close: string }): string | null {
    const trimmed = typeText.trim();
    if (!trimmed) {
        return null;
    }

    const openIndex = trimmed.indexOf(brackets.open);
    const head = (openIndex === -1 ? trimmed : trimmed.slice(0, openIndex)).trim();

    // decision: requires the head to look like a single (possibly dotted/qualified)
    // identifier - rejects function types and tuple types, which contain spaces/parens/`*`
    // and would otherwise be misread as a "domain type" they don't represent.
    if (!/^[A-Za-z_][A-Za-z0-9_.]*$/.test(head)) {
        return null;
    }

    if (NON_DOMAIN_BASE_TYPES.has(head) || isTypeParameterName(head)) {
        return null;
    }

    return head;
}

// Per-function set of distinct base types touched across its typed parameters and return
// type. A function with no typed signals at all returns an empty set - that's "no data
// point", not "different type", and is treated as such by typeCohesionResult below.
export function collectTypeSignals(fn: any, language: LanguageAdapter): Set<string> {
    const types = new Set<string>();

    const paramsNode = findParametersNode(fn, language.nodeTypes.parameters);
    if (paramsNode) {
        for (const child of paramsNode.children) {
            const extracted = language.extractTypedParameter(child);
            if (extracted) {
                const base = baseTypeName(extracted.type, language.genericBrackets);
                if (base) {
                    types.add(base);
                }
            }
        }
    }

    const returnType = language.extractReturnType(fn);
    if (returnType) {
        const base = baseTypeName(returnType, language.genericBrackets);
        if (base) {
            types.add(base);
        }
    }

    return types;
}

export interface TypeCohesionResult {
    // 'insufficient-data' when too few functions carry any type annotation to trust this
    // signal at all - callers should fall back to a naming-based heuristic instead of
    // treating a handful of coincidentally same-typed functions as proof of cohesion.
    result: 'insufficient-data' | boolean;
    // Number of distinct base types observed across typed functions - used by callers to
    // report "spans N unrelated types" when result is confidently false.
    distinctTypes: number;
}

// decision: measures cohesion as a type-*diversity ratio* (distinct base types / typed
// functions), not "does one type dominate" - a single-dominant-type check was tried first
// and rejected after testing against a real F#-style module (expression/collections/seq.py):
// its 80 typed functions span Iterable/Seq/Iterator - three closely related sequence types,
// no single one reaching a 60%+ share - which a one-dominant-type check misreads as
// diversity when it's actually reuse of a small, related type vocabulary. The diversity
// ratio captures that correctly (seq.py: 8 distinct types / 80 typed functions = 0.10,
// clearly cohesive) without needing to know in advance how many "related" types a cohesive
// module is allowed to use.
export interface TypeCohesionThresholds {
    maxDiversityRatio: number;
    minCoverage: number;
}

// decision: takes its two thresholds as a named options object rather than two positional
// numbers - both are plain 0-1 ratios, so adjacent positional params would themselves be
// exactly the primitive-obsession swap-risk this project's own detector flags.
export function typeCohesionResult(
    functions: any[],
    language: LanguageAdapter,
    { maxDiversityRatio, minCoverage }: TypeCohesionThresholds
): TypeCohesionResult {
    const perFunctionTypes = functions.map((fn) => collectTypeSignals(fn, language));
    const typedFunctions = perFunctionTypes.filter((types) => types.size > 0);

    const coverage = functions.length === 0 ? 0 : typedFunctions.length / functions.length;
    if (coverage < minCoverage) {
        return { result: 'insufficient-data', distinctTypes: 0 };
    }

    const distinctTypes = new Set<string>();
    for (const types of typedFunctions) {
        for (const type of types) {
            distinctTypes.add(type);
        }
    }

    const diversityRatio = distinctTypes.size / typedFunctions.length;

    return { result: diversityRatio <= maxDiversityRatio, distinctTypes: distinctTypes.size };
}
