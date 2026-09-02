module Energy.Core.Detectors.ImportCoherence

open Energy.Core

let private importCountThreshold = 10
let private highImportCountThreshold = 15
let private memberImportCountThreshold = 10
let private fSharpSiblingOpenThreshold = 5

// F# `open` brings a module's members into lexical scope. Five or more siblings under one parent
// are therefore a different signal from unrelated dependencies: their values can shadow each other
// and an unqualified reference no longer says where it came from.
let private mostOpenedFSharpSiblingNamespace (importSources: Set<string>) : (string * int) option =
    importSources
    |> Set.toList
    |> List.choose (fun source ->
        let lastSeparator = source.LastIndexOf('.')

        if lastSeparator > 0 && lastSeparator < source.Length - 1 then
            Some(source.Substring(0, lastSeparator))
        else
            None)
    |> List.countBy id
    |> List.filter (fun (_, count) -> count >= fSharpSiblingOpenThreshold)
    |> List.sortByDescending snd
    |> List.tryHead

let private widestMemberImportFanOut (imports: LanguageAdapter.ImportInfo list) : (string * int) option =
    imports
    |> List.filter (fun importInfo -> importInfo.Kind = LanguageAdapter.Members)
    |> List.collect (fun importInfo -> importInfo.Bindings |> List.map (fun _ -> importInfo.Source))
    |> List.countBy id
    |> List.filter (fun (_, count) -> count >= memberImportCountThreshold)
    |> List.sortByDescending snd
    |> List.tryHead

let private importMessage
    (languageId: string)
    (hasWildcard: bool)
    (memberFanOut: (string * int) option)
    (fSharpSiblingNamespace: (string * int) option)
    (importSourceCount: int)
    : string =
    match languageId, memberFanOut, fSharpSiblingNamespace with
    | _, Some(source, bindingCount), _ ->
        sprintf
            "Import member fan-out: %d declarations from %s enter this file's local vocabulary. This is a broad API surface even though it is one dependency; review whether it is an intentional composition boundary, otherwise depend on a smaller cohesive API. Do not replace explicit imports with a wildcard import."
            bindingCount
            source
    | _, _, _ when hasWildcard ->
        "Import scope pollution: a wildcard import makes an entire external scope available without qualification. Prefer explicit imports so symbol origins and collisions remain visible."
    | "fsharp", _, Some(namespaceName, siblingCount) ->
        sprintf
            "Import scope sprawl: %d modules are opened, including %d siblings beneath %s. This is a name-resolution risk: opened values can shadow one another and their origin is unclear. Prefer explicit qualified access or a small named module alias; do not split a cohesive file merely to reduce the count."
            importSourceCount
            siblingCount
            namespaceName
    | _ ->
        sprintf
            "Import sprawl: %d distinct modules create a broad dependency surface. This can mean the file has multiple responsibilities, or that one cohesive capability is exposed through too many sibling modules. Review repeated sibling imports first; introduce a focused facade only when those imports serve one capability, and don't split the file merely to reduce the count."
            importSourceCount

// decision: import breadth, member fan-out, and scope pollution are reported by one focused helper
// so the whole-file traversal remains responsible only for collecting syntax facts. The signals share
// an anchor and severity but need distinct messages because they imply different remediation.
let check
    (imports: LanguageAdapter.ImportInfo list)
    (firstImportNode: TreeSitter.Node option)
    (language: LanguageAdapter.LanguageAdapter)
    (positions: Position.PositionLookup)
    : Violation.EnergyViolation option =
    let importSources = imports |> List.map _.Source |> Set.ofList

    let hasWildcard =
        imports
        |> List.exists (fun importInfo -> importInfo.Kind = LanguageAdapter.Wildcard)

    let fSharpSiblingNamespace =
        if language.Id = "fsharp" then
            mostOpenedFSharpSiblingNamespace importSources
        else
            None

    let memberFanOut = widestMemberImportFanOut imports

    if
        importSources.Count <= importCountThreshold
        && not hasWildcard
        && Option.isNone memberFanOut
        && Option.isNone fSharpSiblingNamespace
    then
        None
    else
        let position =
            match firstImportNode with
            | Some node -> positions.toPosition (TreeSitter.nodeStartIndex node)
            | None -> { Line = 0; Column = 0 }

        let message =
            importMessage language.Id hasWildcard memberFanOut fSharpSiblingNamespace importSources.Count

        Some
            { Line = position.Line
              Column = position.Column
              Type = Violation.Coherence
              Severity =
                if importSources.Count > highImportCountThreshold then
                    Violation.High
                else
                    Violation.Medium
              Message = message
              Hotspots = [] }
