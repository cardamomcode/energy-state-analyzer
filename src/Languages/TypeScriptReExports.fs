// # TypeScriptReExports
//
// TypeScript barrel recognition exempts imports used only to republish declarations. It reads local
// import/export names and requires a declarative file without local implementation. Direct export-from
// syntax needs no imported-binding exemption; mixed implementation files remain visible to coherence
// analysis.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Languages.TypeScriptReExports

open Energy.Core.TreeSitter

/// Read the local names introduced by a TypeScript import clause.
let private importedNames node =
    /// Extract local binding names from one recognized import child, including aliases.
    let namesFromImport item =
        match nodeType item with
        | NodeType "identifier" -> [ nodeText item ]
        | NodeType "namespace_import" -> nodeNamedChildren item |> List.tryLast |> Option.map nodeText |> Option.toList
        | NodeType "named_imports" ->
            nodeNamedChildren item
            |> List.filter (fun child -> nodeType child = NodeType "import_specifier")
            |> List.choose (fun specifier ->
                nodeField "alias" specifier
                |> Option.orElseWith (fun () -> nodeField "name" specifier)
                |> Option.map nodeText)
        | _ -> []

    nodeNamedChildren node
    |> List.filter (fun child -> nodeType child = NodeType "import_clause")
    |> List.collect (fun clause -> nodeNamedChildren clause |> List.collect namesFromImport)

/// Read local names from an export clause; direct export-from statements need no import exemption.
let private locallyExportedNames node =
    if
        nodeType node <> NodeType "export_statement"
        || (nodeField "source" node |> Option.isSome)
    then
        []
    else
        nodeNamedChildren node
        |> List.filter (fun child -> nodeType child = NodeType "export_clause")
        |> List.collect (fun clause ->
            nodeNamedChildren clause
            |> List.choose (fun specifier -> nodeField "name" specifier |> Option.map nodeText))

/// A barrel only contains imports and exports, with no locally implemented declarations.
let private isBarrelItem node =
    match nodeType node with
    | NodeType "import_statement"
    | NodeType "comment" -> true
    | NodeType "export_statement" ->
        (nodeField "source" node |> Option.isSome)
        || (nodeNamedChildren node
            |> List.exists (fun child -> nodeType child = NodeType "export_clause"))
    | _ -> false

/// Recognize an imported binding that is only republished through a declarative export clause.
///
/// decision: require every binding of an import to be re-exported and the file to be a pure barrel;
/// a mixed import remains a dependency because its local-use portion cannot be discarded wholesale.
let isReExport (_fileName: string) node =
    match nodeParent node with
    | Some parent when nodeType parent = NodeType "program" ->
        let items = nodeNamedChildren parent
        let names = importedNames node
        let exports = items |> List.collect locallyExportedNames |> Set.ofList

        (items |> List.forall isBarrelItem)
        && not names.IsEmpty
        && (names |> List.forall exports.Contains)
    | _ -> false
