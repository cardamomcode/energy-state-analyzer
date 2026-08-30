module Energy.Core.Detectors.Cyclomatic

open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context

// Cyclomatic complexity detector.
//
// Flags functions whose cyclomatic complexity exceeds the medium threshold; severity escalates to
// high past the high threshold. Complexity counts independent paths through a function — every
// decision point (if/loop/except/boolean operator/ternary) adds 1, regardless of how deeply it is
// nested. A violation is anchored at the function's start position and carries per-line hotspots
// weighted by nesting depth so callers can paint a heatmap of where the complexity actually piles
// up (the flat metric itself stays unweighted).

type CyclomaticThresholds =
    { MediumThreshold: int
      HighThreshold: int }

// decision: medium 10 / high 15 — the point where holding that many independent paths in working
// memory degrades readability (medium), and deep branching genuinely demands extraction (high).
let defaultCyclomaticThresholds: CyclomaticThresholds =
    { MediumThreshold = 10
      HighThreshold = 15 }

// A decision point is any of: a language-declared decision node type, a boolean operator (and/or —
// matched separately since several grammars reuse one generic binary-expression node for every
// infix operator instead of giving and/or their own), or a try-statement's else clause. Factored
// into one predicate so the complexity count and the hotspot walk agree on exactly what counts; if
// they ever diverged, the two would report different numbers for the same function.
let private isDecisionPoint (language: LanguageAdapter) (node: Node) : bool =
    language.DecisionNodeTypes |> List.contains (nodeType node)
    || language.GetBooleanOperator node |> Option.isSome
    || language.IsTryElseClause node

// decision: complexity is the base 1 plus the number of decision points in the function's subtree.
// A nested named function/method's decision points are never counted toward the enclosing function
// — it is scored as its own separate violation by analyzeFunctionComplexity's traversal, so we stop
// descending into one (the `isRoot` flag keeps the top-level function itself always descending).
// Pure fold of the TS running-counter: this node contributes 1 iff it is a decision point, and every
// child subtree contributes its own count; stopping at a nested function definition drops exactly
// that subtree's children from the sum.
let rec calculateCyclomaticComplexity (language: LanguageAdapter) (node: Node) (isRoot: bool) : int =
    let ownPoint = if isDecisionPoint language node then 1 else 0

    // invariant: a nested named function/method is scored separately, never folded into its parent.
    if not isRoot && language.IsFunctionDefinition node then
        ownPoint
    else
        ownPoint
        + (nodeChildren node
           |> List.sumBy (fun child -> calculateCyclomaticComplexity language child false))

// decision: the base complexity of 1 is added once here; calculateCyclomaticComplexity returns only
// the decision-point count for the given subtree, so callers get the full score with one call.
let complexityOf (language: LanguageAdapter) (functionNode: Node) : int =
    1 + calculateCyclomaticComplexity language functionNode true

// decision: locate every decision point and weight it by nesting depth so callers can render a
// per-line heatmap of where complexity piles up; the flat complexity metric itself stays unweighted.
let rec findCyclomaticHotspots
    (language: LanguageAdapter)
    (positions: PositionLookup)
    (node: Node)
    (depth: int)
    (isRoot: bool)
    : Hotspot list =
    let dp = isDecisionPoint language node

    let thisHotspot: Hotspot list =
        if dp then
            let pos = positions.toPosition (nodeStartIndex node)

            [ { Line = pos.Line; Weight = 1 + depth } ]
        else
            []

    // invariant: mirrors calculateCyclomaticComplexity's traversal exactly — a nested named
    // function/method is hotspotted separately as its own violation, never folded into this one.
    if not isRoot && language.IsFunctionDefinition node then
        thisHotspot
    else
        let nextDepth = if dp then depth + 1 else depth

        // decision: `thisHotspot @ children` preserves the TS pre-order push (parent before its
        // subtree) and left-to-right sibling order, with no accumulator to reverse.
        thisHotspot
        @ (nodeChildren node
           |> List.collect (fun child -> findCyclomaticHotspots language positions child nextDepth false))

let analyzeFunctionComplexity
    (tree: Node)
    (positions: PositionLookup)
    (language: LanguageAdapter)
    (thresholds: CyclomaticThresholds)
    : EnergyViolation list =
    let rec traverse (node: Node) : EnergyViolation list =
        let ownViolations =
            if language.IsFunctionDefinition node then
                let complexity = complexityOf language node

                if complexity > thresholds.MediumThreshold then
                    let pos = positions.toPosition (nodeStartIndex node)

                    let severity =
                        if complexity > thresholds.HighThreshold then
                            High
                        else
                            Medium

                    [ { Line = pos.Line
                        Column = pos.Column
                        Type = Complexity
                        Severity = severity
                        Message =
                          sprintf "High cyclomatic complexity: %d. Consider breaking down this function." complexity
                        Hotspots = findCyclomaticHotspots language positions node 0 true } ]
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
    { Name = "cyclomatic"
      Run = fun ctx -> analyzeFunctionComplexity ctx.Tree ctx.Positions ctx.Language defaultCyclomaticThresholds }
