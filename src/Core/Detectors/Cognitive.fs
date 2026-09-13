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

/// Score a function by walking each of its top-level children at nesting zero.
///
/// decision: score a function by walking each of its top-level children at nesting 0 (the function
/// definition itself is never scored as a decision point — it is the thing being measured). The
/// `contribute` callback records where each increment comes from; scoring passes a no-op.
let cognitiveScoreOf (language: LanguageAdapter) (functionNode: Node) : int =
    nodeChildren functionNode
    |> List.sumBy (fun child -> cognitiveWalk language child 0 (fun _ _ -> ()))

/// Find each scored cognitive point with its line and weight so callers can render a per-line heatmap.
///
/// decision: re-runs the same walk used for scoring, but records where each point of score comes from
/// so callers can render a per-line heatmap across the function body instead of a single flat highlight.
let findCognitiveHotspots (language: LanguageAdapter) (functionNode: Node) (positions: PositionLookup) : Hotspot list =
    let hotspots = ResizeArray()

    nodeChildren functionNode
    |> List.iter (fun child ->
        cognitiveWalk language child 0 (fun node amount ->
            let pos = positions.toPosition (nodeStartIndex node)

            hotspots.Add({ Line = pos.Line; Weight = amount }))
        |> ignore)

    hotspots |> List.ofSeq

/// Report named functions whose cognitive score exceeds the configured threshold.
let analyzeCognitiveComplexity (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse (node: Node) : EnergyViolation list =
        let ownViolations =
            if ctx.Language.IsFunctionDefinition node then
                let complexity = cognitiveScoreOf ctx.Language node

                if complexity > ctx.Options.Cognitive.MediumThreshold then
                    let pos = ctx.Positions.toPosition (nodeStartIndex node)

                    let severity =
                        if complexity > ctx.Options.Cognitive.HighThreshold then
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
                        Hotspots = findCognitiveHotspots ctx.Language node ctx.Positions } ]
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

/// Register cognitive scoring in the shared detector pipeline.
let detector: Detector =
    { Name = "cognitive"
      Run = analyzeCognitiveComplexity }
