module Energy.Languages.RustImports

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Preserve a simple imported name and its lexical binding.
let private simpleImport prefix item =
    let name = nodeText item
    let source = if prefix = "" then name else prefix

    [
        {
            Kind = Module
            Source = source
            Bindings =
                [
                    {
                        ImportedName = name
                        LocalName = name
                    }
                ]
        }
    ]

/// Preserve the original path and the name introduced by `as`.
let private aliasImport prefix item =
    match nodeField "path" item, nodeField "alias" item with
    | Some path, Some alias ->
        let source = if prefix = "" then nodeText path else prefix

        [
            {
                Kind = Members
                Source = source
                Bindings =
                    [
                        {
                            ImportedName = nodeText path
                            LocalName = nodeText alias
                        }
                    ]
            }
        ]
    | _ -> []

/// Attribute a single explicitly imported member to its containing module.
let private memberImport prefix item =
    match nodeField "path" item, nodeField "name" item with
    | Some path, Some name ->
        let source =
            if prefix = "" then
                nodeText path
            else
                prefix + "::" + nodeText path

        let importedName = nodeText name

        [
            {
                Kind = Members
                Source = source
                Bindings =
                    [
                        {
                            ImportedName = importedName
                            LocalName = importedName
                        }
                    ]
            }
        ]
    | _ -> []

/// Retain wildcard scope pollution with its complete source path.
let private wildcardImport prefix item =
    let source =
        nodeNamedChildren item
        |> List.tryHead
        |> Option.map nodeText
        |> Option.defaultValue prefix

    [
        {
            Kind = Wildcard
            Source = source
            Bindings = []
        }
    ]

/// Descend through a Rust `use` tree without losing nested aliases or wildcards.
let rec private readUseTree prefix item =
    match nodeType item with
    | NodeType "scoped_use_list" -> scopedImport prefix item
    | NodeType "use_list" -> nodeNamedChildren item |> List.collect (readUseTree prefix)
    | NodeType "use_as_clause" -> aliasImport prefix item
    | NodeType "scoped_identifier" -> memberImport prefix item
    | NodeType "use_wildcard" -> wildcardImport prefix item
    | _ -> simpleImport prefix item

/// Keep a grouped `use path::{A, B}` as one dependency source with its member bindings.
///
/// decision: the dependency-breadth metric counts the group source once, while a nested
/// wildcard still carries scope-pollution evidence.
and private scopedImport prefix item =
    let source =
        nodeField "path" item |> Option.map nodeText |> Option.defaultValue prefix

    let members =
        nodeField "list" item
        |> Option.map nodeNamedChildren
        |> Option.defaultValue []
        |> List.collect (readUseTree source)

    let kind =
        if members |> List.exists (fun info -> info.Kind = Wildcard) then
            Wildcard
        elif members.IsEmpty then
            Module
        else
            Members

    [
        {
            Kind = kind
            Source = source
            Bindings = members |> List.collect (fun info -> info.Bindings)
        }
    ]

/// Read a Rust `use` declaration as dependency sources and imported local names.
let importInfo node =
    nodeField "argument" node
    |> Option.map (readUseTree "")
    |> Option.defaultValue []
