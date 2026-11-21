// # LogicalControlFlow
//
// This advisory finds recognized boolean AND/OR expressions used directly as expression statements, where
// short-circuiting hides control flow. It does not flag every boolean expression: adapter statement
// ownership distinguishes side-effect sequencing from conditions and computed values. Findings are low
// severity and accumulated through Context.fs.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.Detectors.LogicalControlFlow

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

/// Walk syntax in source order and emit low-severity findings for recognized AND/OR expressions used
/// directly as expression statements. Conditions and value-producing expressions remain quiet; the pipeline
/// owns the enable flag.
let analyzeLogicalControlFlow (ctx: AnalysisContext) : AnalysisContext =
    /// Report a direct statement-level boolean operation before visiting its children in source order.
    let rec traverse node =
        let own =
            match ctx.Language.GetBooleanOperator node, nodeParent node with
            | Some And, Some parent when
                ctx.Language.NodeTypes.ExpressionStatement
                |> Option.exists ((=) (nodeType parent))
                ->
                let position = ctx.Positions.toPosition (nodeStartIndex node)

                [
                    {
                        Line = position.Line
                        Column = position.Column
                        Type = LogicalControlFlow
                        Severity = Low
                        Message = "If-statement disguised as '&&'. Consider an explicit if-statement instead."
                        Hotspots = []
                    }
                ]
            | Some Or, Some parent when
                ctx.Language.NodeTypes.ExpressionStatement
                |> Option.exists ((=) (nodeType parent))
                ->
                let position = ctx.Positions.toPosition (nodeStartIndex node)

                [
                    {
                        Line = position.Line
                        Column = position.Column
                        Type = LogicalControlFlow
                        Severity = Low
                        Message = "If-statement disguised as '||'. Consider an explicit if-statement instead."
                        Hotspots = []
                    }
                ]
            | _ -> []

        own @ (nodeChildren node |> List.collect traverse)

    let findings = traverse ctx.Tree
    addViolations findings ctx

/// Register this detector's stable internal name and context transformation for the shared pipeline;
/// Analyze controls stage ordering.
let detector: Detector =
    {
        Name = "logicalControlFlow"
        Run = analyzeLogicalControlFlow
    }
