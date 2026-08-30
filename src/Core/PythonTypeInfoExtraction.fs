module Energy.Core.PythonTypeInfoExtraction

open Energy.Core.Position
open Energy.Core.PythonTypeInfo
open Energy.Core.PythonTypeInfoClasses
open Energy.Core.PythonTypeInfoFunctions
open Energy.Core.PythonTypeInfoImports
open Energy.Core.TreeSitter

// decision: preserves the prior pre-order AST walk so the logged scaffolding remains stable for
// future consumers even though it currently has no effect on violations.
let extractTypeInformation (tree: Tree) (positions: PositionLookup) : TypeInfo =
    let rec collect node info =
        let updated =
            match nodeType node with
            | NodeType "function_definition" ->
                { info with
                    Functions = info.Functions @ [ extractFunctionTypeInfo positions node ] }
            | NodeType "class_definition" ->
                { info with
                    Classes = info.Classes @ [ extractClassTypeInfo positions node ] }
            | NodeType "assignment" ->
                match extractVariableTypeInfo positions node with
                | Some variable -> { info with Variables = info.Variables @ [ variable ] }
                | None -> info
            | NodeType "import_statement"
            | NodeType "import_from_statement" ->
                { info with
                    Imports = info.Imports @ [ extractImportInfo positions node ] }
            | _ -> info

        nodeChildren node |> List.fold (fun state child -> collect child state) updated

    collect
        (rootNode tree)
        { Functions = []
          Variables = []
          Classes = []
          Imports = [] }
