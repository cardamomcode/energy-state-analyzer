module Energy.Core.Detectors.Cognitive

open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context

// An independent syntax-based implementation modeled on SonarSource's Cognitive Complexity.
// Recursion cycles and Appendix A's compensating function wrappers are not resolved.

/// One part of a boolean expression in source order, with parentheses removed.
type private BooleanPart =
    | Operator of Node * BooleanOperator
    | Operand of Node

/// Anchor an operator run on its token so multiline conditions receive accurate heatmap lines.
let private operatorAnchor node left right =
    nodeChildren node
    |> List.tryFind (fun child ->
        nodeStartIndex child >= nodeEndIndex left
        && nodeEndIndex child <= nodeStartIndex right)
    |> Option.defaultValue node

/// Flatten binary operators in source order, preserving non-boolean operands as separate walks.
///
/// decision: parentheses preserve a sequence; negations and other expressions remain operands so
/// their internal boolean sequences are scored independently.
let rec private booleanParts (language: LanguageAdapter) node =
    match language.GetBooleanOperator node, language.GetCognitiveStructure node with
    | Some operator, _ ->
        let operands = nodeNamedChildren node

        match List.tryHead operands, List.tryLast operands with
        | Some left, Some right when nodeId left <> nodeId right ->
            booleanParts language left
            @ [ Operator(operatorAnchor node left right, operator) ]
            @ booleanParts language right
        | _ -> operands |> List.map Operand
    | None, Some(BooleanGroup inner) -> booleanParts language inner
    | _ -> [ Operand node ]

/// Count operator runs while recursively scoring operands outside the current boolean sequence.
let private scoreBooleanParts walk contribute parts =
    parts
    |> List.fold
        (fun (total, previous) part ->
            match part with
            | Operator(anchor, operator) ->
                let amount = if previous = Some operator then 0 else 1

                if amount > 0 then
                    contribute anchor amount

                total + amount, Some operator
            | Operand operand -> total + walk operand, previous)
        (0, None)
    |> fst

/// Score syntax and record positive contributions through the same walk used by the heatmap.
let rec private cognitiveWalk
    (language: LanguageAdapter)
    (node: Node)
    (nesting: int)
    (contribute: Node -> int -> unit)
    : int =
    let walkChildren nested =
        nodeChildren node
        |> List.sumBy (fun child ->
            let depth = if nested child then nesting + 1 else nesting
            cognitiveWalk language child depth contribute)

    let add amount =
        if amount > 0 then
            contribute node amount

        amount

    match language.GetBooleanOperator node with
    | Some _ ->
        booleanParts language node
        |> scoreBooleanParts (fun operand -> cognitiveWalk language operand nesting contribute) contribute
    | None ->
        match language.GetCognitiveStructure node with
        | Some(Flow(increment, nested)) ->
            let amount =
                match increment with
                | Structural -> 1 + nesting
                | Hybrid
                | Fundamental -> 1

            add amount
            + walkChildren (fun child -> nested |> List.exists (fun body -> nodeId body = nodeId child))
        | Some Closure -> walkChildren (fun _ -> true)
        | _ when language.IsFunctionDefinition node -> walkChildren (fun _ -> true)
        | _ -> walkChildren (fun _ -> false)

/// Select the syntax roots that form one callable body without charging the callable itself.
let private callableBodyRoots (callable: CallableView) =
    if nodeId callable.Anchor = nodeId callable.Body then
        nodeChildren callable.Body
    else
        [ callable.Body ]

/// Score a callable by walking its body at nesting zero.
///
/// decision: score a callable body at nesting 0 (the callable declaration itself is never scored as
/// a decision point — it is the thing being measured). The
/// `contribute` callback records where each increment comes from; scoring passes a no-op.
let cognitiveScoreOf (language: LanguageAdapter) (callable: CallableView) : int =
    callableBodyRoots callable
    |> List.sumBy (fun root -> cognitiveWalk language root 0 (fun _ _ -> ()))

/// Find each scored cognitive point with its line and weight so callers can render a per-line heatmap.
///
/// decision: re-runs the same walk used for scoring, but records where each point of score comes from
/// so callers can render a per-line heatmap across the function body instead of a single flat highlight.
let findCognitiveHotspots
    (language: LanguageAdapter)
    (callable: CallableView)
    (positions: PositionLookup)
    : Hotspot list =
    let hotspots = ResizeArray()

    callableBodyRoots callable
    |> List.iter (fun root ->
        cognitiveWalk language root 0 (fun node amount ->
            let pos = positions.toPosition (nodeStartIndex node)

            hotspots.Add({ Line = pos.Line; Weight = amount }))
        |> ignore)

    hotspots |> List.ofSeq

/// Report one callable when its independent cognitive score exceeds the configured threshold.
let private analyzeCallable (ctx: AnalysisContext) (callable: CallableView) =
    let complexity = cognitiveScoreOf ctx.Language callable

    if complexity > ctx.Options.Cognitive.MediumThreshold then
        let pos = ctx.Positions.toPosition (nodeStartIndex callable.Anchor)

        let severity =
            if complexity > ctx.Options.Cognitive.HighThreshold then
                High
            else
                Medium

        [
            {
                Line = pos.Line
                Column = pos.Column
                Type = Cognitive
                Severity = severity
                Message =
                    sprintf
                        "High cognitive complexity: %d. This function is hard to read; consider flattening nesting or extracting functions."
                        complexity
                Hotspots = findCognitiveHotspots ctx.Language callable ctx.Positions
            }
        ]
    else
        []

/// Report named and anonymous callables whose cognitive score exceeds the configured threshold.
let analyzeCognitiveComplexity (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse (node: Node) : EnergyViolation list =
        let ownViolations =
            ctx.Language.GetCallableViews node |> List.collect (analyzeCallable ctx)

        // decision: prepend this callable's violation ahead of its subtree (ownViolations @ children)
        // so a callable reports before descending into it — matching the TS push-to-end ordering,
        // siblings left to right.
        ownViolations @ (nodeChildren node |> List.collect traverse)

    let findings = traverse ctx.Tree
    addViolations findings ctx

/// Register cognitive scoring in the shared detector pipeline.
let detector: Detector =
    {
        Name = "cognitive"
        Run = analyzeCognitiveComplexity
    }
