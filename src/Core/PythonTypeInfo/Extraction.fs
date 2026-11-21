// # Extraction
//
// Python type-information extraction walks a live parsed tree in pre-order, collecting signatures,
// annotated assignments, class descriptions, and imports. Sibling helpers decode each shape and positions
// become detached zero-based lines. This scaffolding does not contribute findings to Analyze.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.PythonTypeInfo.Extraction

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.PythonTypeInfo.Classes
open Energy.Core.PythonTypeInfo.Functions
open Energy.Core.PythonTypeInfo.Imports
open Energy.Core.TreeSitter

/// Walk a parsed Python AST in pre-order, collecting its functions, classes, variables, and imports.
///
/// decision: preserves the prior pre-order AST walk so the logged scaffolding remains stable for
/// future consumers even though it currently has no effect on violations.
let extractTypeInformation (tree: Tree) (positions: PositionLookup) : TypeInfo =
    /// Append the current declaration's extracted records, then fold through children in source order.
    let rec collect node info =
        let updated =
            match nodeType node with
            | NodeType "function_definition" ->
                { info with
                    Functions = info.Functions @ [ extractFunctionTypeInfo positions node ]
                }
            | NodeType "class_definition" ->
                { info with
                    Classes = info.Classes @ [ extractClassTypeInfo positions node ]
                }
            | NodeType "assignment" ->
                match extractVariableTypeInfo positions node with
                | Some variable ->
                    { info with
                        Variables = info.Variables @ [ variable ]
                    }
                | None -> info
            | NodeType "import_statement"
            | NodeType "import_from_statement" ->
                { info with
                    Imports = info.Imports @ [ extractImportInfo positions node ]
                }
            | _ -> info

        nodeChildren node |> List.fold (fun state child -> collect child state) updated

    collect
        (rootNode tree)
        {
            Functions = []
            Variables = []
            Classes = []
            Imports = []
        }
