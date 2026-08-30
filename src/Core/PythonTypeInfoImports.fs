module Energy.Core.PythonTypeInfoImports

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.TreeSitter

let private plainImport children =
    let items =
        children
        |> List.filter (fun child ->
            nodeType child = NodeType "dotted_name" || nodeType child = NodeType "identifier")
        |> List.map nodeText

    { Module = items |> List.tryHead |> Option.defaultValue ""
      Items = items
      Line = 0 }

let private fromImport children =
    let beforeImport =
        children
        |> List.takeWhile (fun child -> nodeText child <> "import")
        |> List.skipWhile (fun child -> nodeText child <> "from")
        |> List.skip 1

    let moduleName =
        beforeImport
        |> List.tryFind (fun child ->
            nodeType child = NodeType "dotted_name" || nodeType child = NodeType "identifier")
        |> Option.map nodeText
        |> Option.defaultValue ""

    let items =
        children
        |> List.skipWhile (fun child -> nodeText child <> "import")
        |> List.skip 1
        |> List.filter (fun child -> nodeType child = NodeType "identifier")
        |> List.map nodeText

    { Module = moduleName; Items = items; Line = 0 }

let extractImportInfo (positions: PositionLookup) node =
    let position = positions.toPosition (nodeStartIndex node)

    let result =
        match nodeType node with
        | NodeType "import_statement" -> plainImport (nodeChildren node)
        | NodeType "import_from_statement" -> fromImport (nodeChildren node)
        | _ ->
            { Module = ""
              Items = []
              Line = 0 }

    { result with Line = position.Line }
