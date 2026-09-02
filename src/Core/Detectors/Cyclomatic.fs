module Energy.Core.Detectors.Cyclomatic


open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context

// Cyclomatic complexity detector.
//
// Flags functions whose cyclomatic complexity exceeds the medium threshold; severity escalates to
// high past the high threshold. Complexity is McCabe's V(G) = E - N + 2P over a reduced control-
// flow graph: non-branching code is collapsed, while every control-flow alternative remains an
// edge. A violation is anchored at the function's start position and carries per-line hotspots
// weighted by nesting depth so callers can paint a heatmap of where complexity piles up.

// decision: cyclomatic thresholds live in Core.Config as the single source of truth; this detector
// reads them from ctx.Options so it no longer re-exports a module-level copy.

type private FlowNode =
    | Entry
    | Exit
    | Decision of int

type private FunctionScope =
    | RootFunction
    | NestedFunction

type private FlowEdge = { From: FlowNode; To: FlowNode }

type private ControlFlowGraph =
    { Nodes: FlowNode list
      Edges: FlowEdge list
      ExitPredecessors: FlowNode list }

// decision: represents a reduced control-flow multigraph so the reported value remains McCabe's
// E - N + 2P while irrelevant straight-line statements do not inflate the graph.
// invariant: every current path to Exit appears once in ExitPredecessors and has one matching edge.
let private initialGraph =
    { Nodes = [ Entry; Exit ]
      Edges = [ { From = Entry; To = Exit } ]
      ExitPredecessors = [ Entry ] }

// decision: names the fixed terms in McCabe's E - N + 2P so the calculation stays recognisable
// without treating its mathematical constants as unexplained literals.
// invariant: every function graph is connected, so P is exactly one.
let private connectedComponents = 1
let private mccabeComponentMultiplier = 2

let private addDecision (outcomes: int) (graph: ControlFlowGraph) : ControlFlowGraph =
    let decision = Decision graph.Nodes.Length

    let continuingEdges =
        graph.ExitPredecessors
        |> List.map (fun predecessor -> { From = predecessor; To = decision })

    let outcomeEdges = List.replicate outcomes { From = decision; To = Exit }

    { Nodes = decision :: graph.Nodes
      Edges =
        (graph.Edges |> List.filter (fun edge -> edge.To <> Exit))
        @ continuingEdges
        @ outcomeEdges
      ExitPredecessors = List.replicate outcomes decision }

// A decision point is any of: a language-declared decision node type, a boolean operator (and/or —
// matched separately since several grammars reuse one generic binary-expression node for every
// infix operator instead of giving and/or their own), or a try-statement's else clause. Factored
// into one predicate so graph construction and the hotspot walk agree on exactly what counts.
let private isDecisionPoint (language: LanguageAdapter) (node: Node) : bool =
    language.DecisionNodeTypes |> List.contains (nodeType node)
    || language.GetBooleanOperator node |> Option.isSome
    || language.IsTryElseClause node

let private decisionOutcomes (language: LanguageAdapter) (node: Node) : int =
    if language.GetBooleanOperator node |> Option.isSome then
        2
    else
        language.CyclomaticBranchCount node |> Option.defaultValue 2

// A nested named function/method's graph is never folded into its parent: it is scored separately
// by analyzeFunctionComplexity's traversal. Each decision replaces the graph's current Exit edges
// with its outcomes, which preserves a connected graph and makes multi-way branches explicit.
let rec private buildControlFlowGraph
    (language: LanguageAdapter)
    (node: Node)
    (scope: FunctionScope)
    (graph: ControlFlowGraph)
    : ControlFlowGraph =
    let nextGraph =
        if isDecisionPoint language node then
            addDecision (decisionOutcomes language node) graph
        else
            graph

    // invariant: a nested named function/method is scored separately, never folded into its parent.
    if scope = NestedFunction && language.IsFunctionDefinition node then
        nextGraph
    else
        nodeChildren node
        |> List.fold (fun current child -> buildControlFlowGraph language child NestedFunction current) nextGraph

// decision: calculates McCabe complexity from the explicit reduced graph rather than treating an
// AST decision count as the metric. Each function graph has one connected component (P = 1).
let complexityOf (language: LanguageAdapter) (functionNode: Node) : int =
    let graph = buildControlFlowGraph language functionNode RootFunction initialGraph

    graph.Edges.Length - graph.Nodes.Length
    + mccabeComponentMultiplier * connectedComponents

// decision: locate every decision point and weight it by nesting depth so callers can render a
// per-line heatmap of where complexity piles up; multi-way branches carry their full contribution.
let rec private findCyclomaticHotspots
    (language: LanguageAdapter)
    (positions: PositionLookup)
    (node: Node)
    (depth: int)
    (scope: FunctionScope)
    : Hotspot list =
    let dp = isDecisionPoint language node

    let thisHotspot: Hotspot list =
        if dp then
            let pos = positions.toPosition (nodeStartIndex node)

            let contribution = decisionOutcomes language node - 1

            [ { Line = pos.Line
                Weight = contribution * (1 + depth) } ]
        else
            []

    // invariant: mirrors buildControlFlowGraph's traversal exactly — a nested named
    // function/method is hotspotted separately as its own violation, never folded into this one.
    if scope = NestedFunction && language.IsFunctionDefinition node then
        thisHotspot
    else
        let nextDepth = if dp then depth + 1 else depth

        // decision: `thisHotspot @ children` preserves the TS pre-order push (parent before its
        // subtree) and left-to-right sibling order, with no accumulator to reverse.
        thisHotspot
        @ (nodeChildren node
           |> List.collect (fun child -> findCyclomaticHotspots language positions child nextDepth NestedFunction))

let analyzeFunctionComplexity (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse (node: Node) : EnergyViolation list =
        let ownViolations =
            if ctx.Language.IsFunctionDefinition node then
                let complexity = complexityOf ctx.Language node

                if complexity > ctx.Options.Cyclomatic.MediumThreshold then
                    let pos = ctx.Positions.toPosition (nodeStartIndex node)

                    let severity =
                        if complexity > ctx.Options.Cyclomatic.HighThreshold then
                            High
                        else
                            Medium

                    [ { Line = pos.Line
                        Column = pos.Column
                        Type = Complexity
                        Severity = severity
                        Message =
                          sprintf "High cyclomatic complexity: %d. Consider breaking down this function." complexity
                        Hotspots = findCyclomaticHotspots ctx.Language ctx.Positions node 0 RootFunction } ]
                else
                    []
            else
                []

        // decision: prepend this function's violation ahead of its subtree (ownViolations @ children)
        // so a function reports before descending into it — matching the TS push-to-end ordering,
        // siblings left to right.
        ownViolations @ (nodeChildren node |> List.collect traverse)

    let findings = traverse ctx.Tree
    addViolations findings ctx

let detector: Detector =
    { Name = "cyclomatic"
      Run = analyzeFunctionComplexity }
