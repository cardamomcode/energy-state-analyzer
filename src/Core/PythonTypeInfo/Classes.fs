module Energy.Core.PythonTypeInfo.Classes

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.PythonTypeInfo.Functions
open Energy.Core.TreeSitter

let private baseClasses node =
    childOfType "argument_list" node
    |> Option.map (
        nodeChildren
        >> List.filter (fun child -> nodeType child = NodeType "identifier")
        >> List.map nodeText
    )
    |> Option.defaultValue []

let private typedDictFields positions node =
    node
    |> Option.map nodeChildren
    |> Option.defaultValue []
    |> List.choose (fun statement ->
        if nodeType statement <> NodeType "expression_statement" then
            None
        else
            childOfType "assignment" statement
            |> Option.bind (extractVariableTypeInfo positions))

let extractClassTypeInfo (positions: PositionLookup) node =
    let bases = baseClasses node
    let isTypedDict = List.contains "TypedDict" bases
    let position = positions.toPosition (nodeStartIndex node)

    { Name =
        childOfType "identifier" node
        |> Option.map nodeText
        |> Option.defaultValue "unknown"
      Line = position.Line
      BaseClasses = bases
      IsTypedDict = isTypedDict
      Fields =
        if isTypedDict then
            typedDictFields positions (childOfType "block" node)
        else
            [] }
