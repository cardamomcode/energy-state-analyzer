module Energy.Core.Detectors.MatchOpportunity

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

type MatchOpportunityThresholds = { MinBranches: int }

let defaultThresholds = { MinBranches = 3 }

let private hasType expected node =
    expected |> Option.exists ((=) (nodeType node))

let private stripQuotes (text: string) =
    if text.Length >= 2 then
        text.Substring(1, text.Length - 2)
    else
        text

let private collectChainBranches (language: LanguageAdapter) (ifNode: Node) =
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

            match nested with
            | Some next -> loop next (next :: branches)
            | None -> List.rev branches

        loop ifNode [ ifNode ]

let private collectDiscriminants (language: LanguageAdapter) (otherBranchIds: Set<int>) (branch: Node) =
    let isVariable node =
        List.contains (nodeType node) language.VariableReferenceNodeTypes

    let isLiteral node =
        hasType language.NodeTypes.StringLiteral node
        || hasType language.NodeTypes.IntegerLiteral node
        || hasType language.NodeTypes.FloatLiteral node

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
                    if isVariable comparison.Left && isLiteral comparison.Right then
                        [ nodeText comparison.Left, literalValue comparison.Right ]
                    elif isVariable comparison.Right && isLiteral comparison.Left then
                        [ nodeText comparison.Right, literalValue comparison.Left ]
                    else
                        [])

            let memberships =
                language.GetMembershipComparisons node
                |> List.collect (fun comparison ->
                    if isVariable comparison.Left then
                        comparison.Values |> List.map (fun value -> nodeText comparison.Left, value)
                    else
                        [])

            equalities @ memberships @ (nodeChildren node |> List.collect walk)

    walk branch

let analyzeMatchOpportunities
    (tree: Node)
    (positions: PositionLookup)
    (language: LanguageAdapter)
    (thresholds: MatchOpportunityThresholds)
    =
    let rec traverse (consumed: Set<int>) (node: Node) : EnergyViolation list =
        let own, consumed =
            if
                hasType language.NodeTypes.IfStatement node
                && not (consumed.Contains(nodeId node))
            then
                let branches = collectChainBranches language node

                let updated =
                    branches
                    |> List.tail
                    |> List.fold (fun (state: Set<int>) branch -> state.Add(nodeId branch)) consumed

                if branches.Length < thresholds.MinBranches then
                    [], updated
                else
                    let discriminants =
                        branches
                        |> List.map (fun branch ->
                            collectDiscriminants
                                language
                                (branches
                                 |> List.filter (fun other -> nodeId other <> nodeId branch)
                                 |> List.map nodeId
                                 |> Set.ofList)
                                branch)

                    let common =
                        match discriminants with
                        | first :: rest ->
                            first
                            |> List.map fst
                            |> List.tryFind (fun variable ->
                                rest |> List.forall (List.exists (fun (candidate, _) -> candidate = variable)))
                        | [] -> None

                    match common with
                    | Some variable when discriminants |> List.forall (List.isEmpty >> not) ->
                        let position = positions.toPosition (nodeStartIndex node)

                        [ { Line = position.Line
                            Column = position.Column
                            Type = MatchOpportunity
                            Severity = Low
                            Message =
                              sprintf
                                  "This %d-way if/elif chain all branch on '%s'. Consider a match/switch statement for clearer, exhaustiveness-checked dispatch."
                                  branches.Length
                                  variable
                            Hotspots = [] } ],
                        updated
                    | _ -> [], updated
            else
                [], consumed

        own @ (nodeChildren node |> List.collect (traverse consumed))

    traverse Set.empty tree

let detector: Detector =
    { Name = "matchOpportunity"
      Run = fun ctx -> analyzeMatchOpportunities ctx.Tree ctx.Positions ctx.Language defaultThresholds }
