module Energy.Languages.PythonAdapterSyntax

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Python grammar node name for membership and relational comparisons.
let private comparisonOperatorNodeType = NodeType "comparison_operator"

/// Python grammar node name for imports that select members from a source module.
let private importFromStatementNodeType = NodeType "import_from_statement"

/// Text prefix before the source name in a Python member import.
let private fromPrefix = "from "

/// Text separator between the source and member names in a Python member import.
let private importSeparator = " import "

/// Text prefix before the module names in a plain Python import.
let private importPrefix = "import "

/// Number of tokens in an imported-member alias such as `value as local`.
let private aliasedImportPartCount = 3

/// Collect string values from a Python tuple, list, or set only when every named child is a string.
///
/// decision: skips unnamed punctuation before checking literal shape because separators and
/// brackets are structural tokens, not membership values.
/// decision: carries failure in tuple state rather than an option accumulator because Fable cannot
/// lower the latter inside the nested List.fold used here.
let private collectStringValues (collection: Node) =
    let step (failed: bool, values: string list) (child: Node) =
        if failed then
            true, values
        elif not (nodeIsNamed child) then
            false, values
        elif nodeType child <> NodeType "string" then
            true, values
        else
            let text = nodeText child
            false, values @ [ text.Substring(1, text.Length - 2) ]

    let failed, values = nodeChildren collection |> List.fold step (false, [])

    if failed || values.IsEmpty then None else Some values

/// Convert one membership-operator position into a normalized comparison when its right side is a literal collection.
let private membershipComparison (children: Node list) (leftIndex, rightIndex) =
    let left = children.[leftIndex]
    let right = children.[rightIndex]

    let isCollection =
        List.contains (nodeType right) [ NodeType "tuple"; NodeType "list"; NodeType "set" ]

    if isCollection then
        collectStringValues right
        |> Option.map (fun values -> { Left = left; Values = values })
    else
        None

/// Extract Python `value in ("a", "b")` comparisons for primitive-obsession analysis.
let membershipComparisons (node: Node) : MembershipComparison list =
    if nodeType node <> comparisonOperatorNodeType then
        []
    else
        let children = nodeChildren node

        children
        |> List.mapi (fun index child -> index, child)
        |> List.filter (fun (index, _) -> index >= 1 && index + 1 < children.Length)
        |> List.filter (fun (_, child) -> nodeType child = NodeType "in")
        |> List.map (fun (index, _) -> index - 1, index + 1)
        |> List.choose (membershipComparison children)

/// Parse one imported member and its optional local alias.
let private importBinding (name: string) =
    let parts = name.Split([| ' ' |], System.StringSplitOptions.RemoveEmptyEntries)
    let imported = parts.[0]

    let local =
        if parts.Length >= aliasedImportPartCount && parts.[1] = "as" then
            parts.[2]
        else
            imported

    {
        ImportedName = imported
        LocalName = local
    }

/// Build member or wildcard import information from one valid `from ... import ...` statement.
let private importedMembers (text: string) (importIndex: int) =
    let source =
        text.Substring(fromPrefix.Length, importIndex - fromPrefix.Length).Trim()

    let names =
        text.Substring(importIndex + importSeparator.Length).Trim().Trim([| '('; ')' |])

    if names = "*" then
        [
            {
                Kind = Wildcard
                Source = source
                Bindings = []
            }
        ]
    else
        let bindings =
            names.Split(',')
            |> Array.toList
            |> List.map (fun name -> name.Trim())
            |> List.filter (fun name -> name <> "")
            |> List.map importBinding

        [
            {
                Kind = Members
                Source = source
                Bindings = bindings
            }
        ]

/// Parse a Python `from source import names` statement.
let private importFromInfo (node: Node) =
    let text = nodeText node
    let importIndex = text.IndexOf(importSeparator, System.StringComparison.Ordinal)

    if importIndex > fromPrefix.Length then
        importedMembers text importIndex
    else
        [
            {
                Kind = Members
                Source = text
                Bindings = []
            }
        ]

/// Normalize one plain module import while discarding its local alias from the source identity.
let private moduleImport (item: string) =
    let parts =
        item.Trim().Split([| ' ' |], System.StringSplitOptions.RemoveEmptyEntries)

    {
        Kind = Module
        Source = parts.[0]
        Bindings = []
    }

/// Normalize Python module, member, alias, and wildcard imports for coherence analysis.
let importInfo (node: Node) : ImportInfo list =
    if nodeType node = importFromStatementNodeType then
        importFromInfo node
    else
        nodeText node
        |> fun text -> text.Substring(importPrefix.Length).Split(',')
        |> Array.toList
        |> List.map moduleImport
