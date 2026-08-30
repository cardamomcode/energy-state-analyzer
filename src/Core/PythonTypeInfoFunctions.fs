module Energy.Core.PythonTypeInfoFunctions

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.TreeSitter

let private parameterInfo hasDefault node =
    { Name =
        childOfType "identifier" node
        |> Option.map nodeText
        |> Option.defaultValue "unknown"
      Type = childOfType "type" node |> Option.map extractTypeString
      HasDefault = hasDefault }

let private extractParameters node =
    nodeChildren node
    |> List.choose (fun child ->
        match nodeType child with
        | NodeType "typed_parameter" -> Some(parameterInfo false child)
        | NodeType "default_parameter" -> Some(parameterInfo true child)
        | NodeType "identifier" ->
            Some
                { Name = nodeText child
                  Type = None
                  HasDefault = false }
        | _ -> None)

let private returnType node =
    nodeChildren node
    |> List.pairwise
    |> List.tryPick (function
        | arrow, annotation when nodeText arrow = "->" && nodeType annotation = NodeType "type" ->
            Some(extractTypeString annotation)
        | _ -> None)

let extractFunctionTypeInfo (positions: PositionLookup) node =
    let position = positions.toPosition (nodeStartIndex node)

    { Name =
        childOfType "identifier" node
        |> Option.map nodeText
        |> Option.defaultValue "unknown"
      Line = position.Line
      Parameters =
        childOfType "parameters" node
        |> Option.map extractParameters
        |> Option.defaultValue []
      ReturnType = returnType node }

let extractVariableTypeInfo (positions: PositionLookup) node =
    match childOfType "identifier" node, childOfType "type" node with
    | Some identifier, Some annotation ->
        let position = positions.toPosition (nodeStartIndex node)

        Some
            { Name = nodeText identifier
              Type = extractTypeString annotation
              Line = position.Line }
    | _ -> None
