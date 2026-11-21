// # MatchOpportunity
//
// Conditional chains become match suggestions when enough branches compare a shared variable with
// recognized literals. MatchOpportunitySupport.fs excludes branch bodies from evidence and normalizes
// grammar-specific chains. Consumed branch identities prevent a chain from being reported again as each
// nested else-if is traversed; advice does not prove exhaustiveness.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.Detectors.MatchOpportunity


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Detectors.MatchOpportunitySupport

/// Build a low-severity violation for an if/elif chain that branches on one variable.
///
/// decision: matchOpportunity thresholds live in Core.Config as the single source of truth; this
/// detector reads them from ctx.Options so it no longer re-exports a module-level copy.

let private matchOpportunityViolation
    (positions: PositionLookup)
    (node: Node)
    (branches: Node list)
    (variable: string)
    : EnergyViolation =
    let position = positions.toPosition (nodeStartIndex node)

    {
        Line = position.Line
        Column = position.Column
        Type = MatchOpportunity
        Severity = Low
        Message =
            sprintf
                "This %d-way if/elif chain all branch on '%s'. Consider a match/switch statement for clearer, exhaustiveness-checked dispatch."
                branches.Length
                variable
        Hotspots = []
    }

/// Walk conditional chains, tracking consumed branch IDs to avoid duplicate chain reports. Require the
/// configured branch count and a variable compared with recognized literals in every branch before adding a
/// low-severity match suggestion.
let analyzeMatchOpportunities (ctx: AnalysisContext) : AnalysisContext =
    /// Recognize an unconsumed conditional chain, carry its branch identities into descendants, and
    /// retain parent-before-child findings.
    let rec traverse (consumed: Set<int>) (node: Node) : EnergyViolation list =
        let own, consumed =
            if
                hasType ctx.Language.NodeTypes.IfStatement node
                && not (consumed.Contains(nodeId node))
            then
                let branches = collectChainBranches ctx.Language node

                let updated =
                    branches
                    |> List.tail
                    |> List.fold (fun (state: Set<int>) (branch: Node) -> state.Add(nodeId branch)) consumed

                let discriminants =
                    branches
                    |> List.map (fun branch ->
                        collectDiscriminants ctx.Language (otherBranchIds branch branches) branch)

                let violation =
                    discriminants
                    |> List.forall (List.isEmpty >> not)
                    |> function
                        | true when branches.Length >= ctx.Options.MatchOpportunity.MinBranches ->
                            commonDiscriminant discriminants
                            |> Option.map (matchOpportunityViolation ctx.Positions node branches)
                        | _ -> None

                violation |> Option.toList, updated
            else
                [], consumed

        own @ (nodeChildren node |> List.collect (traverse consumed))

    let findings = traverse Set.empty ctx.Tree
    addViolations findings ctx

/// Register this detector's stable internal name and context transformation for the shared pipeline;
/// Analyze controls stage ordering.
let detector: Detector =
    {
        Name = "matchOpportunity"
        Run = analyzeMatchOpportunities
    }
