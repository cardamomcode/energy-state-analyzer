// # Imports
//
// Import extraction records simple Python module and item text with source lines. It assumes the
// recognized from-import grammar contains its required keywords and does not resolve aliases or imported
// symbols. This legacy type-information record is separate from the richer LanguageAdapter import
// contract.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.PythonTypeInfo.Imports

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.TreeSitter

/// Collect direct dotted names and identifiers from a plain import, using the first as its module and a
/// placeholder line until the caller attaches the location.
let private plainImport children =
    let items =
        children
        |> List.filter (fun child ->
            nodeType child = NodeType "dotted_name"
            || nodeType child = NodeType "identifier")
        |> List.map nodeText

    {
        Module = items |> List.tryHead |> Option.defaultValue ""
        Items = items
        Line = 0
    }

/// Name the keyword that separates a from-import's module from its imported names.
let private importKeyword = "import"

/// Extract a from-import's module and direct imported identifiers; callers supply well-formed syntax
/// containing both keywords. Aliased or nested forms are not resolved.
let private fromImport children =
    let beforeImport =
        children
        |> List.takeWhile (fun child -> nodeText child <> importKeyword)
        |> List.skipWhile (fun child -> nodeText child <> "from")
        |> List.skip 1

    let moduleName =
        beforeImport
        |> List.tryFind (fun child ->
            nodeType child = NodeType "dotted_name"
            || nodeType child = NodeType "identifier")
        |> Option.map nodeText
        |> Option.defaultValue ""

    let items =
        children
        |> List.skipWhile (fun child -> nodeText child <> importKeyword)
        |> List.skip 1
        |> List.filter (fun child -> nodeType child = NodeType "identifier")
        |> List.map nodeText

    {
        Module = moduleName
        Items = items
        Line = 0
    }

/// Extract recognized plain or from-import syntax and attach its zero-based line. Other node kinds produce
/// empty module and item data at that location.
let extractImportInfo (positions: PositionLookup) node =
    let position = positions.toPosition (nodeStartIndex node)

    let result =
        match nodeType node with
        | NodeType "import_statement" -> plainImport (nodeChildren node)
        | NodeType "import_from_statement" -> fromImport (nodeChildren node)
        | _ -> { Module = ""; Items = []; Line = 0 }

    { result with Line = position.Line }
