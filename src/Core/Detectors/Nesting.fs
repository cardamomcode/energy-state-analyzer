module Energy.Core.Detectors.Nesting

open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context

// Nesting detector (port of src/core/detectors/nesting.ts).
//
// Flags control-flow nodes (if/for/while/with/try/match, per the language adapter) nested deeper
// than the medium threshold; severity escalates to high past the high threshold. Each violation is
// anchored at the control node's start position.

type NestingThresholds =
    { MediumThreshold: int
      HighThreshold: int }

// decision: default medium threshold of 3 is the point where tracking active conditions starts to
// strain working memory; high threshold of 5 escalates severity for the deepest offenders.
let defaultNestingThresholds: NestingThresholds =
    { MediumThreshold = 3
      HighThreshold = 5 }

let analyzeNesting (ctx: AnalysisContext) (thresholds: NestingThresholds) : EnergyViolation list =
    // Pure pre-order DFS that reproduces the TS algorithm's push-to-end ordering exactly: a control
    // node emits its own violation before descending into its children, and siblings are visited
    // left to right. `myViol @ childResults` places this node's violation ahead of its subtree while
    // preserving sibling order — no accumulator threading (which would reverse sibling order) needed.
    let rec traverse (node: Node) (depth: int) : EnergyViolation list =
        let isControl = ctx.Language.NestingControlTypes |> List.contains (nodeType node)
        let nextDepth = if isControl then depth + 1 else depth

        // decision: flatten each child's violations into one list — a subtree contributes all of its
        // descendants' violations, not a nested list per child. `List.collect` maps then concatenates.
        let childResults = nodeChildren node |> List.collect (fun c -> traverse c nextDepth)

        if isControl && depth > thresholds.MediumThreshold then
            let pos = ctx.Positions.toPosition (nodeStartIndex node)
            let severity = if depth > thresholds.HighThreshold then High else Medium

            let v =
                { Line = pos.Line
                  Column = pos.Column
                  Type = Nesting
                  Severity = severity
                  Message = sprintf "Excessive nesting depth: %d. Consider extracting." depth
                  Hotspots = [] }

            // decision: prepend this node's violation ahead of its subtree (v :: childResults == [ v ] @ childResults)
            // so a control node reports before descending — matching the TS push-to-end ordering, siblings left to right.
            v :: childResults
        else
            childResults

    traverse ctx.Tree 0

let detector: Detector =
    { Name = "nesting"
      Run = fun ctx -> analyzeNesting ctx defaultNestingThresholds }
