module Energy.Core.Detectors.OpaqueBoolean

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

let analyzeOpaqueBooleanLiteral (tree: Node) (positions: PositionLookup) (language: LanguageAdapter) =
    let rec walk node =
        let own =
            if language.IsBooleanLiteral node && language.IsPositionalCallArgument node then
                let position = positions.toPosition (nodeStartIndex node)

                [ { Line = position.Line
                    Column = position.Column
                    Type = OpaqueBoolean
                    Severity = Low
                    Message =
                      sprintf
                          "Opaque boolean literal: a bare '%s' passed positionally tells the reader nothing without checking the callee's signature. Name it at the call site (a keyword argument, an object-literal field, or F#'s named-argument syntax) — or better, split into two clearly named functions (e.g. enableX()/disableX()) or use an enum."
                          (nodeText node)
                    Hotspots = [] } ]
            else
                []

        own @ (nodeChildren node |> List.collect walk)

    walk tree

let detector: Detector =
    { Name = "opaqueBoolean"
      Run = fun ctx -> analyzeOpaqueBooleanLiteral ctx.Tree ctx.Positions ctx.Language }

let handler: Energy.Core.AnalysisPipeline.AnalysisHandler =
    Energy.Core.AnalysisPipeline.detector detector.Run
