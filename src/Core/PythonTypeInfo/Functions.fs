// # Functions
//
// Python signature extraction reads explicit parameter and return annotations and annotated assignments
// without type inference. Unsupported parameter shapes are omitted and absent names use an unknown
// placeholder. Extraction.fs uses these detached records; the current detector adapter has its own richer
// callable normalization.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.PythonTypeInfo.Functions

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.TreeSitter

/// Named boolean flags for whether a Python parameter carries a default value.
///
/// decision: name the two `hasDefault` values so the call sites read self-descriptively instead of
/// passing bare true/false whose meaning only survives by reading `parameterInfo`'s signature.
let private parameterHasDefault = true
/// Mark a recognized parameter as having no default value.
let private parameterNoDefault = false

/// Build a parameter description from direct name and type children and the supplied default flag; missing
/// names become unknown.
let private parameterInfo hasDefault node =
    {
        Name =
            childOfType "identifier" node
            |> Option.map nodeText
            |> Option.defaultValue "unknown"
        Type = childOfType "type" node |> Option.map extractTypeString
        HasDefault = hasDefault
    }

/// Collect typed, default, and bare identifier parameters in source order. Unrecognized shapes, including
/// typed-default nodes here, are omitted.
let private extractParameters node =
    nodeChildren node
    |> List.choose (fun child ->
        match nodeType child with
        | NodeType "typed_parameter" -> Some(parameterInfo parameterNoDefault child)
        | NodeType "default_parameter" -> Some(parameterInfo parameterHasDefault child)
        | NodeType "identifier" ->
            Some
                {
                    Name = nodeText child
                    Type = None
                    HasDefault = false
                }
        | _ -> None)

/// Find an arrow followed by a type annotation among direct children, returning None if that syntax is
/// absent.
let private returnType node =
    nodeChildren node
    |> List.pairwise
    |> List.tryPick (function
        | arrow, annotation when nodeText arrow = "->" && nodeType annotation = NodeType "type" ->
            Some(extractTypeString annotation)
        | _ -> None)

/// Extract a function's zero-based line and recognized signature pieces; missing parameters become an empty
/// list and a missing name becomes unknown.
let extractFunctionTypeInfo (positions: PositionLookup) node =
    let position = positions.toPosition (nodeStartIndex node)

    {
        Name =
            childOfType "identifier" node
            |> Option.map nodeText
            |> Option.defaultValue "unknown"
        Line = position.Line
        Parameters =
            childOfType "parameters" node
            |> Option.map extractParameters
            |> Option.defaultValue []
        ReturnType = returnType node
    }

/// Extract an assignment only when both a direct identifier and explicit type annotation exist; otherwise
/// return None.
let extractVariableTypeInfo (positions: PositionLookup) node =
    match childOfType "identifier" node, childOfType "type" node with
    | Some identifier, Some annotation ->
        let position = positions.toPosition (nodeStartIndex node)

        Some
            {
                Name = nodeText identifier
                Type = extractTypeString annotation
                Line = position.Line
            }
    | _ -> None
