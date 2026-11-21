// # PythonTypeInfo
//
// These records describe Python syntax-derived signatures, annotations, class bases, and imports for the
// extraction helpers. They are scaffolding rather than a compiler symbol table and are not consumed by the
// registered detector pipeline. Extraction.fs owns the pre-order collection; Functions.fs, Classes.fs, and
// Imports.fs decode individual declarations.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.PythonTypeInfo

open Energy.Core.TreeSitter

/// Python parameter information extracted from recognized signature syntax. Type is absent when
/// no explicit annotation is found; HasDefault records the recognized default-value form.

type ParameterTypeInfo =
    {
        Name: string
        Type: string option
        HasDefault: bool
    }

/// Syntactic function signature with zero-based declaration line, recognized parameters, and optional
/// return annotation; missing annotations are not inferred.
type FunctionTypeInfo =
    {
        Name: string
        Line: int
        Parameters: ParameterTypeInfo list
        ReturnType: string option
    }

/// An explicitly annotated assignment's name, textual type, and zero-based source line.
type VariableTypeInfo =
    {
        Name: string
        Type: string
        Line: int
    }

/// Class name, line, and simple base names; Fields contains annotated assignments only when a literal
/// TypedDict base is recognized.
type ClassTypeInfo =
    {
        Name: string
        Line: int
        BaseClasses: string list
        IsTypedDict: bool
        Fields: VariableTypeInfo list
    }

/// Syntactic module and imported names with a zero-based source line; this scaffolding does not resolve
/// symbols.
type ImportInfo =
    {
        Module: string
        Items: string list
        Line: int
    }

/// Collected Python signatures, annotated variables, classes, and imports in traversal order; it is not a
/// type-checked symbol table.
type TypeInfo =
    {
        Functions: FunctionTypeInfo list
        Variables: VariableTypeInfo list
        Classes: ClassTypeInfo list
        Imports: ImportInfo list
    }

/// Find the first direct child with the requested grammar kind; descendants are not searched.
let childOfType name node =
    nodeChildren node |> List.tryFind (fun child -> nodeType child = NodeType name)

/// Unwrap single-child type nodes and reconstruct recognized generics from their arguments. Other shapes
/// retain literal source text rather than attempting type inference.
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
