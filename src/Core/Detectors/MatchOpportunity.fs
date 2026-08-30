module Energy.Core.Detectors.MatchOpportunity

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Detectors.MatchOpportunitySupport

type MatchOpportunityThresholds = { MinBranches: int }

let defaultThresholds = { MinBranches = 3 }

let private matchOpportunityViolation
    (positions: PositionLookup)
    (node: Node)
    (branches: Node list)
    (variable: string)
    : EnergyViolation =
    let position = positions.toPosition (nodeStartIndex node)

    { Line = position.Line
      Column = position.Column
      Type = MatchOpportunity
      Severity = Low
      Message =
        sprintf
            "This %d-way if/elif chain all branch on '%s'. Consider a match/switch statement for clearer, exhaustiveness-checked dispatch."
            branches.Length
            variable
      Hotspots = [] }

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
                    |> List.fold (fun (state: Set<int>) (branch: Node) -> state.Add(nodeId branch)) consumed

                let discriminants =
                    branches
                    |> List.map (fun branch -> collectDiscriminants language (otherBranchIds branch branches) branch)

                let violation =
                    discriminants
                    |> List.forall (List.isEmpty >> not)
                    |> function
                        | true when branches.Length >= thresholds.MinBranches ->
                            commonDiscriminant discriminants
                            |> Option.map (matchOpportunityViolation positions node branches)
                        | _ -> None

                violation |> Option.toList, updated
            else
                [], consumed

        own @ (nodeChildren node |> List.collect (traverse consumed))

    traverse Set.empty tree

let detector: Detector =
    { Name = "matchOpportunity"
      Run = fun ctx -> analyzeMatchOpportunities ctx.Tree ctx.Positions ctx.Language defaultThresholds }
