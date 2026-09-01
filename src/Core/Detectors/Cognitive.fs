module Energy.Core.Detectors.Cognitive


open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context

// Cognitive complexity (SonarSource): unlike cyclomatic complexity, every decision point is
// weighted by how deeply it is nested, and early-return guard clauses are not penalized. This
// tracks how hard code is to *read*, not just how many paths it has.
//
// decision: implements a simplified subset of the SonarSource spec rather than the full algorithm,
// acceptable for a first pass — each assumption below is called out at its point of use:
//   - `for`/`while` `else` clauses (where a grammar has them) are scored like `if`/`else`, even
//     though they aren't really a decision point.
//   - boolean-operator chain merging ("a and b and c" = one increment) only checks the immediate
//     parent's operator, not the full chain direction.
//   - recursive calls to the enclosing function are not specially detected.
//   - match/switch-like constructs and try/except are scored once as a whole, not per-case — see
//     each LanguageAdapter for the exact node-type mapping (CognitiveNestedDecisionTypes).

type CognitiveThresholds = Energy.Core.Context.CognitiveThresholds

// decision: medium 15 / high 25 — cognitive complexity weights nesting more heavily than cyclomatic,
// so its thresholds sit higher (SonarSource's own defaults); a function that is only "medium" by
// cyclomatic can legitimately be clean here.
let defaultCognitiveThresholds: CognitiveThresholds =
    defaultAnalyzeOptions.Cognitive

// decision: compare a node against an optional grammar node type without leaking `option` into the
// detectors — a None field (a grammar gap) degrades to "never matches", so the corresponding check
// simply never fires instead of needing a guard at every call site.
let private hasNodeType (t: NodeType option) (node: Node) : bool =
    match t with
    | Some ty -> nodeType node = ty
    | None -> false

type private CognitiveNodeKind =
    | BooleanOperator of int
    | NestedDecision
    | ElseClause
    | ConditionalExpression
    | FunctionDefinition
    | Lambda
    | Other

let private classifyNode (language: LanguageAdapter) (node: Node) : CognitiveNodeKind =
    let operatorContribution operator =
        nodeParent node
        |> Option.bind language.GetBooleanOperator
        |> Option.filter ((<>) operator)
        |> Option.map (fun _ -> 1)
        |> Option.defaultValue (if Option.isNone (nodeParent node) then 1 else 0)

    match language.GetBooleanOperator node with
    | Some operator -> BooleanOperator(operatorContribution operator)
    | None when List.contains (nodeType node) language.CognitiveNestedDecisionTypes -> NestedDecision
    | None when hasNodeType language.NodeTypes.ElseClause node -> ElseClause
    | None when hasNodeType language.NodeTypes.ConditionalExpression node -> ConditionalExpression
    | None when language.IsFunctionDefinition node -> FunctionDefinition
    | None when hasNodeType language.NodeTypes.Lambda node -> Lambda
    | None -> Other

// decision: the cognitive walk scores a node and then descends, branching on what kind of node it is.
// A `rec ... and` pair mirrors the TS `walk`/`walkNested`: `cognitiveWalk` handles one node (and its
// own children), while `cognitiveWalkChild` wraps a single child by first deciding whether that child
// enters nested scope — the only place depth is incremented for if/for/while bodies.
let rec private cognitiveWalk
    (language: LanguageAdapter)
    (node: Node)
    (nesting: int)
    (contribute: Node -> int -> unit)
    : int =
    let walkChildren walker =
        nodeChildren node
        |> List.sumBy (fun child -> walker language child nesting contribute)

    let contributeAndWalk contribution walker =
        contribute node contribution
        contribution + walkChildren walker

    match classifyNode language node with
    | BooleanOperator contribution -> contributeAndWalk contribution cognitiveWalk
    | NestedDecision -> contributeAndWalk (1 + nesting) cognitiveWalkChild
    | ElseClause -> contributeAndWalk 1 cognitiveWalkChild
    | ConditionalExpression ->
        contribute node (1 + nesting)

        1
        + nesting
        + (nodeChildren node
           |> List.sumBy (fun child -> cognitiveWalk language child (nesting + 1) contribute))
    | FunctionDefinition ->
        let contribution = 1 + nesting
        contribute node contribution
        contribution
    | Lambda ->
        // decision: unlike NestedDecision, a lambda/closure is not itself a scored decision point
        // (SonarSource's spec only scores if/for/while/switch/catch/logical chains) — it only raises
        // the nesting level for whatever decision points live inside it. Nesting bumps unconditionally
        // here (rather than via cognitiveWalkChild's EntersNestedScope gate) because several grammars
        // (Kotlin's lambda_literal, Python's lambda, TS's expression-bodied arrow) hold their body as
        // direct children with no wrapping block node, so the gate would never fire for them.
        nodeChildren node
        |> List.sumBy (fun child -> cognitiveWalk language child (nesting + 1) contribute)
    | Other -> walkChildren cognitiveWalk

and private cognitiveWalkChild
    (language: LanguageAdapter)
    (child: Node)
    (nesting: int)
    (contribute: Node -> int -> unit)
    : int =
    // a child enters nested scope only where the language says so (Python/TS have an explicit body
    // node; F# has none, so every child is nested content). The increment applies to the walk's depth.
    let nextNesting =
        if language.EntersNestedScope child then
            nesting + 1
        else
            nesting

    cognitiveWalk language child nextNesting contribute

// decision: score a function by walking each of its top-level children at nesting 0 (the function
// definition itself is never scored as a decision point — it is the thing being measured). The
// `contribute` callback records where each increment comes from; scoring passes a no-op.
let cognitiveScoreOf (language: LanguageAdapter) (functionNode: Node) : int =
    nodeChildren functionNode
    |> List.sumBy (fun child -> cognitiveWalk language child 0 (fun _ _ -> ()))

// decision: re-runs the same walk used for scoring, but records where each point of score comes from
// so callers can render a per-line heatmap across the function body instead of a single flat highlight.
let findCognitiveHotspots (language: LanguageAdapter) (functionNode: Node) (positions: PositionLookup) : Hotspot list =
    let hotspots = ResizeArray()

    nodeChildren functionNode
    |> List.iter (fun child ->
        cognitiveWalk language child 0 (fun node amount ->
            let pos = positions.toPosition (nodeStartIndex node)

            hotspots.Add({ Line = pos.Line; Weight = amount }))
        |> ignore)

    hotspots |> List.ofSeq

let analyzeCognitiveComplexity
    (tree: Node)
    (positions: PositionLookup)
    (language: LanguageAdapter)
    (thresholds: CognitiveThresholds)
    : EnergyViolation list =
    let rec traverse (node: Node) : EnergyViolation list =
        let ownViolations =
            if language.IsFunctionDefinition node then
                let complexity = cognitiveScoreOf language node

                if complexity > thresholds.MediumThreshold then
                    let pos = positions.toPosition (nodeStartIndex node)

                    let severity =
                        if complexity > thresholds.HighThreshold then
                            High
                        else
                            Medium

                    [ { Line = pos.Line
                        Column = pos.Column
                        Type = Cognitive
                        Severity = severity
                        Message =
                          sprintf
                              "High cognitive complexity: %d. This function is hard to read; consider flattening nesting or extracting functions."
                              complexity
                        Hotspots = findCognitiveHotspots language node positions } ]
                else
                    []
            else
                []

        // decision: prepend this function's violation ahead of its subtree (ownViolations @ children)
        // so a function reports before descending into it — matching the TS push-to-end ordering,
        // siblings left to right.
        ownViolations @ (nodeChildren node |> List.collect traverse)

    traverse tree

let detector: Detector =
    { Name = "cognitive"
      Run =
        fun ctx ->
            analyzeCognitiveComplexity ctx.Tree ctx.Positions ctx.Language ctx.Options.Cognitive
            |> addViolations
            <| ctx }
