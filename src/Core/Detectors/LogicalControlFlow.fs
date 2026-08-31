module Energy.Core.Detectors.LogicalControlFlow


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

let analyzeLogicalControlFlow (tree: Node) (positions: PositionLookup) (language: LanguageAdapter) =
    let rec traverse node =
        let own =
            match language.GetBooleanOperator node, nodeParent node with
            | Some And, Some parent when language.NodeTypes.ExpressionStatement |> Option.exists ((=) (nodeType parent)) ->
                let position = positions.toPosition (nodeStartIndex node)

                [ { Line = position.Line
                    Column = position.Column
                    Type = LogicalControlFlow
                    Severity = Low
                    Message = "If-statement disguised as '&&'. Consider an explicit if-statement instead."
                    Hotspots = [] } ]
            | Some Or, Some parent when language.NodeTypes.ExpressionStatement |> Option.exists ((=) (nodeType parent)) ->
                let position = positions.toPosition (nodeStartIndex node)

                [ { Line = position.Line
                    Column = position.Column
                    Type = LogicalControlFlow
                    Severity = Low
                    Message = "If-statement disguised as '||'. Consider an explicit if-statement instead."
                    Hotspots = [] } ]
            | _ -> []

        own @ (nodeChildren node |> List.collect traverse)

    traverse tree

let detector: Detector =
    { Name = "logicalControlFlow"
      Run = fun ctx -> analyzeLogicalControlFlow ctx.Tree ctx.Positions ctx.Language }

let handler: Energy.Core.AnalysisPipeline.AnalysisHandler =
    Energy.Core.AnalysisPipeline.detector detector.Run
