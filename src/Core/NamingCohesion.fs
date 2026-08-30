module Energy.Core.NamingCohesion

open System
open System.Text.RegularExpressions

open Energy.Core.TreeSitter

// Port of src/core/namingCohesion.ts — the naming side of the file-coherence detector.
//
// These helpers decide whether a set of functions (or class names) reads as "one coherent domain
// factored into many small steps" rather than a grab-bag of unrelated helpers, by looking for a
// dominant leading or trailing word shared across most of the names. They are pure string/AST
// analysis with no tree-sitter state, so they are reused both by coherence's function-count check
// (looksLikeSingleDomain) and by classRelatedness's naming-affix fallback (looksLikeSingleDomainByNames).

// decision: splits on underscores AND camelCase/acronym boundaries (extractFoo -> [extract, foo],
// parse_json -> [parse, json], URLParser -> [url, parser]) rather than a plain leading `[a-z]+` run,
// so a word boundary is recognized regardless of the file's naming convention.
let private wordBoundaryPattern =
    Regex("[A-Z]+(?=[A-Z][a-z])|[A-Z]?[a-z]+|[A-Z]+|[0-9]+")

let private splitIntoWords (text: string) : string list =
    let matches = wordBoundaryPattern.Matches text

    if matches.Count = 0 then
        []
    else
        [ for m in matches -> m.Value.ToLowerInvariant() ]

// decision: splits the basename into words (on `_` and camelCase boundaries) and requires an exact
// word match, rather than a substring `includes` check — `includes('common')` would also match an
// unrelated file like `commonwealth.ts`, and `includes('util')` would match `futuleName.ts`.
let private utilsFileWords =
    Set.ofList [ "util"; "utils"; "helper"; "helpers"; "common" ]

let isUtilsFileName (fileName: string) : bool =
    let baseName = fileName.Split('/') |> Array.tryLast |> Option.defaultValue ""
    // strip a trailing `.ext` — everything after the last dot, if any.
    let withoutExtension =
        match baseName.LastIndexOf('.') with
        | -1 -> baseName
        | idx -> baseName.Substring(0, idx)

    splitIntoWords withoutExtension
    |> List.exists (fun w -> utilsFileWords.Contains w)

// decision: a raw function name is a weak signal on its own, but a *dominant leading or trailing
// word* shared across most of a file's functions (extractFoo/extractBar, or fooParser/barParser) is
// a cheap, AST-only proxy for "this file is one coherent domain factored into many small steps" —
// exactly the case a raw function-count sprawl check would otherwise misflag. Checking both ends also
// catches naming conventions that put the domain word last (parseDate/formatDate), not just first.
let private functionNameWords (node: Node) : string list =
    let nameNode =
        nodeChildren node |> List.tryFind (fun c -> nodeType c = NodeType "identifier")

    match nameNode with
    | Some n when not (String.IsNullOrEmpty(nodeText n)) -> splitIntoWords (nodeText n)
    | _ -> []

let private dominantShare (words: string list) : float =
    if words.Length = 0 then
        0.0
    else
        let wordCounts = System.Collections.Generic.Dictionary<string, int>()

        for w in words do
            let current = if wordCounts.ContainsKey w then wordCounts.[w] else 0

            wordCounts.[w] <- current + 1

        float (wordCounts.Values |> Seq.max) / float words.Length

// decision: shared by looksLikeSingleDomain (functions) and looksLikeSingleDomainByNames (classes,
// see coherence.ts's checkClassRelatedness) — both want "does a dominant leading or trailing
// word-boundary chunk recur across most of these names", just starting from a different source.
let looksLikeSingleDomainByNames (names: string list) (minShare: float) : bool =
    let leadingWords = ResizeArray<string>()
    let trailingWords = ResizeArray<string>()

    for name in names do
        let words = splitIntoWords name

        if words.Length > 0 then
            leadingWords.Add(words.[0])
            trailingWords.Add(words.[words.Length - 1])

    if leadingWords.Count = 0 then
        false
    else
        dominantShare (List.ofSeq leadingWords) >= minShare
        || dominantShare (List.ofSeq trailingWords) >= minShare

let looksLikeSingleDomain (functions: Node list) (minShare: float) : bool =
    let functionWordStrings =
        functions |> List.map (fun fn -> functionNameWords fn |> String.concat " ")

    looksLikeSingleDomainByNames functionWordStrings minShare
