module Energy.Core.Detectors.LogicalControlFlow


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

let analyzeLogicalControlFlow (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse node =
        let own =
            match ctx.Language.GetBooleanOperator node, nodeParent node with
            | Some And, Some parent when
                ctx.Language.NodeTypes.ExpressionStatement
                |> Option.exists ((=) (nodeType parent))
                ->
                let position = ctx.Positions.toPosition (nodeStartIndex node)

                [ { Line = position.Line
                    Column = position.Column
                    Type = LogicalControlFlow
                    Severity = Low
                    Message = "If-statement disguised as '&&'. Consider an explicit if-statement instead."
                    Hotspots = [] } ]
            | Some Or, Some parent when
                ctx.Language.NodeTypes.ExpressionStatement
                |> Option.exists ((=) (nodeType parent))
                ->
                let position = ctx.Positions.toPosition (nodeStartIndex node)

                [ { Line = position.Line
                    Column = position.Column
                    Type = LogicalControlFlow
                    Severity = Low
                    Message = "If-statement disguised as '||'. Consider an explicit if-statement instead."
                    Hotspots = [] } ]
            | _ -> []

        own @ (nodeChildren node |> List.collect traverse)

    let findings = traverse ctx.Tree
    addViolations findings ctx

let detector: Detector =
    { Name = "logicalControlFlow"
      Run = analyzeLogicalControlFlow }
