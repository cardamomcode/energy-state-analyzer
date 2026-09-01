module Energy.Core.Detectors.MatchOpportunitySupport

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

let hasType expected node =
    expected |> Option.exists ((=) (nodeType node))

let stripQuotes (text: string) =
    if text.Length >= 2 then
        text.Substring(1, text.Length - 2)
    else
        text

let collectChainBranches (language: LanguageAdapter) (ifNode: Node) =
    let flat = language.GetElseIfBranches ifNode

    if flat.Length > 0 then
        ifNode :: flat
    else
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

let collectDiscriminants (language: LanguageAdapter) (otherBranchIds: Set<int>) (branch: Node) =
    let isVariable node =
        List.contains (nodeType node) language.VariableReferenceNodeTypes

    let isLiteral node = language.IsMatchCaseLiteral node

    let literalValue node =
        if hasType language.NodeTypes.StringLiteral node then
            stripQuotes (nodeText node)
        else
            nodeText node

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

let otherBranchIds (branch: Node) (branches: Node list) =
    branches
    |> List.filter (fun other -> nodeId other <> nodeId branch)
    |> List.map nodeId
    |> Set.ofList

let commonDiscriminant (discriminants: (string * string) list list) =
    match discriminants with
    | first :: rest ->
        first
        |> List.map fst
        |> List.tryFind (fun variable -> rest |> List.forall (List.exists (fun (candidate, _) -> candidate = variable)))
    | [] -> None
