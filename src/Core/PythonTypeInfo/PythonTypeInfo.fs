module Energy.Core.PythonTypeInfo

open Energy.Core.TreeSitter

// Python-only type-information scaffolding. It is intentionally independent of VS Code and is
// logged by the extension without affecting detector output.

type ParameterTypeInfo =
    { Name: string
      Type: string option
      HasDefault: bool }

type FunctionTypeInfo =
    { Name: string
      Line: int
      Parameters: ParameterTypeInfo list
      ReturnType: string option }

type VariableTypeInfo =
    { Name: string
      Type: string
      Line: int }

type ClassTypeInfo =
    { Name: string
      Line: int
      BaseClasses: string list
      IsTypedDict: bool
      Fields: VariableTypeInfo list }

type ImportInfo =
    { Module: string
      Items: string list
      Line: int }

type TypeInfo =
    { Functions: FunctionTypeInfo list
      Variables: VariableTypeInfo list
      Classes: ClassTypeInfo list
      Imports: ImportInfo list }

let childOfType name node =
    nodeChildren node |> List.tryFind (fun child -> nodeType child = NodeType name)

let rec extractTypeString node =
    match nodeType node, nodeChildren node with
    | NodeType "type", [ child ] -> extractTypeString child
    | NodeType "generic_type", _ ->
        let baseType = childOfType "identifier" node |> Option.map nodeText

        let parameters =
            childOfType "type_parameter" node
            |> Option.map (
                nodeChildren
                >> List.filter (fun child -> nodeType child = NodeType "type")
                >> List.map extractTypeString
            )
            |> Option.defaultValue []

        match baseType with
        | Some name when not parameters.IsEmpty -> name + "[" + String.concat ", " parameters + "]"
        | _ -> nodeText node

    // decision: catch-all keeps the recursion total — any other node shape (a bare identifier, a
    // reference, or a `type` node with more than one child) falls back to its literal text instead of
    // leaving an uncovered case for Fable to warn about and for the runtime to blow up on.
    | _ -> nodeText node
