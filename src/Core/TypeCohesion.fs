module Energy.Core.TypeCohesion

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter
open Energy.Core.Detectors.ParameterCount

// Port of src/core/typeCohesion.ts — the type side of the file-coherence detector.
//
// Whereas namingCohesion looks at function *names*, this measures cohesion as a type-*diversity*
// ratio across a set of functions: few distinct base types reused across many typed functions means
// one cohesive module; many unrelated types means sprawl. It is pure text analysis over the type
// annotations the LanguageAdapter already extracts, so it shares no tree-sitter state and is reused
// both by coherence's function-count check (typeCohesionResult) and by classRelatedness's type
// cross-reference signal (collectTypeSignals).

// decision: excluded outright rather than counted as a "domain type" — each of these names the *shape*
// of a value (a callback, an absent/untyped result), not what a function operates on. Confirmed
// necessary empirically: running this detector against a real F#-style Python module initially still
// misfired because ~45% of its functions take a Callable callback alongside their real domain type —
// left uncounted, Callable would have out-voted the actual dominant type (Iterable).
let private nonDomainBaseTypes = Set.ofList [ "Callable"; "Function"; "Any"; "None"; "Unit"; "void" ]

let private singleUpperPattern = Regex("^[A-Z]$")
let private genericTypeParameterPattern = Regex("^_?T([A-Z]\\w*|\\d*)$")

// PEP-484/TS/Kotlin generic type-parameter naming conventions (bare `T`/`U`/`K`/`V`, or Python's
// leading-underscore `_TSource`/`_TState`/`_T1` convention) — these name "the same generic slot", not
// a concrete type, and are excluded for the same reason as NON_DOMAIN_BASE_TYPES above.
let private isTypeParameterName (name: string) : bool =
    singleUpperPattern.IsMatch(name) || genericTypeParameterPattern.IsMatch(name)

// Strips generic type arguments from a raw type-text blob down to a comparable base type name, e.g.
// "Iterable<T>" -> "Iterable", "Iterable[_TSource]" -> "Iterable". Returns null for shapes that aren't
// a plain (possibly dotted/qualified) named type — function types ("(x: T) => U"), tuple types
// ("int * string") — since those don't represent "this function operates on domain type X" and
// guessing would produce noise rather than signal. Also returns null for NON_DOMAIN_BASE_TYPES and
// type-parameter-shaped names.
//
// known gap: wrapper generics (Optional[str], Dict[str, int]) normalize to their wrapper base
// (Optional, Dict), not the wrapped domain type — same for F#'s postfix `int option` syntax, which has
// no bracket at all and is rejected outright by the identifier check below. Left as a documented v1
// limitation; unwrapping common wrappers per language would reopen the per-language special-casing
// this shared, text-based helper is designed to avoid.
let baseTypeName (typeText: string) (brackets: GenericBrackets) : string option =
    let trimmed = typeText.Trim()

    if String.IsNullOrEmpty trimmed then
        None
    else
        let openIndex = trimmed.IndexOf(brackets.Open)
        // the head is everything before the first bracket — or the whole thing if there's no bracket.
        let head =
            if openIndex = -1 then
                trimmed.Trim()
            else
                trimmed.Substring(0, openIndex).Trim()

        // decision: requires the head to look like a single (possibly dotted/qualified) identifier —
        // rejects function types and tuple types, which contain spaces/parens/`*` and would otherwise be
        // misread as a "domain type" they don't represent.
        if not (Regex.IsMatch(head, "^[A-Za-z_][A-Za-z0-9_.]*$")) then
            None
        elif nonDomainBaseTypes.Contains head || isTypeParameterName head then
            None
        else
            Some head

// Per-function set of distinct base types touched across its typed parameters and return type. A
// function with no typed signals at all returns an empty set — that's "no data point", not "different
// type", and is treated as such by typeCohesionResult below.
let collectTypeSignals (fn: Node) (language: LanguageAdapter) : HashSet<string> =
    let types = HashSet<string>()

    match findParametersNode fn language.NodeTypes.Parameters with
    | Some paramsNode ->
        for child in nodeChildren paramsNode do
            match language.ExtractTypedParameter child with
            | Some tp ->
                match baseTypeName tp.Type language.GenericBrackets with
                | Some baseType -> types.Add(baseType) |> ignore
                | None -> ()
            | None -> ()
    | None -> ()

    match language.ExtractReturnType fn with
    | Some returnType ->
        match baseTypeName returnType language.GenericBrackets with
        | Some baseType -> types.Add(baseType) |> ignore
        | None -> ()
    | None -> ()

    types

// A measured type-cohesion result: `Result` is the cohesion verdict (true = cohesive), `DistinctTypes`
// is the number of distinct base types observed, used by callers to report "spans N unrelated types".
type MeasuredTypeCohesion = { Result: bool; DistinctTypes: int }

type TypeCohesionResult =
    // 'insufficient-data' when too few functions carry any type annotation to trust this signal at all
    // — callers should fall back to a naming-based heuristic instead of treating a handful of
    // coincidentally same-typed functions as proof of cohesion.
    | InsufficientData
    // A measured result: `Result` is the cohesion verdict (true = cohesive), `DistinctTypes` is the
    // number of distinct base types observed, used by callers to report "spans N unrelated types".
    | Measured of MeasuredTypeCohesion

type TypeCohesionThresholds = { MaxDiversityRatio: float; MinCoverage: float }

// decision: measures cohesion as a type-*diversity* ratio (distinct base types / typed functions), not
// "does one type dominate" — a single-dominant-type check was tried first and rejected after testing
// against a real F#-style module (expression/collections/seq.py): its 80 typed functions span
// Iterable/Seq/Iterator — three closely related sequence types, no single one reaching a 60%+ share —
// which a one-dominant-type check misreads as diversity when it's actually reuse of a small, related
// type vocabulary. The diversity ratio captures that correctly (seq.py: 8 distinct types / 80 typed
// functions = 0.10, clearly cohesive) without needing to know in advance how many "related" types a
// cohesive module is allowed to use.
let typeCohesionResult (functions: Node list) (language: LanguageAdapter) (thresholds: TypeCohesionThresholds) : TypeCohesionResult =
    let maxDiversityRatio = thresholds.MaxDiversityRatio
    let minCoverage = thresholds.MinCoverage
    let perFunctionTypes = functions |> List.map (fun fn -> collectTypeSignals fn language)
    let typedFunctions = perFunctionTypes |> List.filter (fun s -> s.Count > 0)

    let coverage = if functions.Length = 0 then 0.0 else float typedFunctions.Length / float functions.Length

    if coverage < minCoverage then
        InsufficientData
    else
        let distinctTypes = HashSet<string>()

        for types in typedFunctions do
            for t in types do
                distinctTypes.Add(t) |> ignore

        let diversityRatio = float distinctTypes.Count / float typedFunctions.Length
        let measuredResult = { Result = diversityRatio <= maxDiversityRatio; DistinctTypes = distinctTypes.Count }

        Measured measuredResult
