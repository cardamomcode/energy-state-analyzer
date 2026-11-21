// # MatchOpportunitySupport
//
// These syntax helpers collect conditional-chain branches and the variable/literal evidence in their
// conditions. They exclude other branches and executable bodies, retain source order, and select a
// variable shared by all branch evidence. MatchOpportunity.fs owns thresholds and finding construction.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.Detectors.MatchOpportunitySupport

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Test a node against an optional grammar kind; an unsupported kind never matches.
let hasType expected node =
    expected |> Option.exists ((=) (nodeType node))

/// Remove the first and last characters of text at least two characters long; shorter text is returned
/// unchanged.
let stripQuotes (text: string) =
    if text.Length >= 2 then
        text.Substring(1, text.Length - 2)
    else
        text

/// Collect a conditional and its else-if branches in source order. Prefer an adapter's flat branch list;
/// otherwise follow nested else/if grammar shapes until the chain ends.
let collectChainBranches (language: LanguageAdapter) (ifNode: Node) =
    let flat = language.GetElseIfBranches ifNode

    if flat.Length > 0 then
        ifNode :: flat
    else
        /// Follow nested else-if syntax, reversing the local accumulator once when the chain ends.
        let rec loop current branches =
            let nested =
                match language.NodeTypes.ElseClause with
                | Some _ ->
                    nodeChildren current
                    |> List.tryFind (hasType language.NodeTypes.ElseClause)
                    |> Option.bind (fun elseClause ->
                        nodeChildren elseClause |> List.tryFind (hasType language.NodeTypes.IfStatement))
                | None -> nodeChildren current |> List.tryFind (hasType language.NodeTypes.IfStatement)

            nested
            |> Option.map (fun next -> loop next (next :: branches))
            |> Option.defaultValue (List.rev branches)

        loop ifNode [ ifNode ]

/// Collect variable/literal equality and membership pairs from a branch condition. Skip other chain
/// branches, blocks, and else clauses so body comparisons cannot supply the discriminant.
let collectDiscriminants (language: LanguageAdapter) (otherBranchIds: Set<int>) (branch: Node) =
    /// Accept only adapter-declared variable-reference syntax.
    let isVariable node =
        List.contains (nodeType node) language.VariableReferenceNodeTypes

    /// Accept only literals the adapter permits as match or switch cases.
    let isLiteral node = language.IsMatchCaseLiteral node

    /// Compare string cases without their surrounding quotes and other literals by their source text.
    let literalValue node =
        if hasType language.NodeTypes.StringLiteral node then
            stripQuotes (nodeText node)
        else
            nodeText node

    /// Collect condition comparisons while pruning other branches, body blocks, and else clauses.
    let rec walk node =
        if
            otherBranchIds.Contains(nodeId node)
            || hasType language.NodeTypes.Block node
            || hasType language.NodeTypes.ElseClause node
        then
            []
        else
            let equalities =
                language.GetEqualityComparisons node
                |> List.collect (fun comparison ->
                    match
                        isVariable comparison.Left,
                        isLiteral comparison.Right,
                        isVariable comparison.Right,
                        isLiteral comparison.Left
                    with
                    | true, true, _, _ -> [ nodeText comparison.Left, literalValue comparison.Right ]
                    | _, _, true, true -> [ nodeText comparison.Right, literalValue comparison.Left ]
                    | _ -> [])

            let memberships =
                language.GetMembershipComparisons node
                |> List.collect (fun comparison ->
                    comparison.Values
                    |> List.map (fun value -> nodeText comparison.Left, value)
                    |> List.filter (fun _ -> isVariable comparison.Left))

            equalities @ memberships @ (nodeChildren node |> List.collect walk)

    walk branch

/// Return identities of the other branches in a chain, allowing condition traversal to exclude their
/// subtrees.
let otherBranchIds (branch: Node) (branches: Node list) =
    branches
    |> List.filter (fun other -> nodeId other <> nodeId branch)
    |> List.map nodeId
    |> Set.ofList

/// Return the first variable from the first branch that appears in every remaining branch; empty input or
/// no shared variable returns None.
let commonDiscriminant (discriminants: (string * string) list list) =
    match discriminants with
    | first :: rest ->
        first
        |> List.map fst
        |> List.tryFind (fun variable -> rest |> List.forall (List.exists (fun (candidate, _) -> candidate = variable)))
    | [] -> None
