// # Classes
//
// Class extraction records simple base identifiers and source locations. A literal TypedDict base enables
// direct annotated-field collection; other classes expose no fields in this scaffolding. Functions.fs
// decodes those assignments, and Extraction.fs collects the result without affecting detector findings.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.PythonTypeInfo.Classes

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.PythonTypeInfo.Functions
open Energy.Core.TreeSitter

/// Extract simple identifier bases from a class argument list; qualified or more complex bases are not
/// inferred.
let private baseClasses node =
    childOfType "argument_list" node
    |> Option.map (
        nodeChildren
        >> List.filter (fun child -> nodeType child = NodeType "identifier")
        >> List.map nodeText
    )
    |> Option.defaultValue []

/// Collect annotated assignments directly inside an optional class block; missing bodies and other
/// statement shapes contribute no fields.
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

/// Extract class identity and simple bases, recognizing TypedDict by its literal base name. Only that case
/// collects fields; missing names become unknown.
let extractClassTypeInfo (positions: PositionLookup) node =
    let bases = baseClasses node
    let isTypedDict = List.contains "TypedDict" bases
    let position = positions.toPosition (nodeStartIndex node)

    {
        Name =
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
                []
    }
