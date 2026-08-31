module Energy.Core.DetectorPipelinePatterns

open Energy.Core.AnalysisPipeline
open Energy.Core.DetectorPipeline

let handler (thresholds: AnalyzeThresholds) : AnalysisHandler =
    Detectors.PrimitiveObsession.handler
    |> compose Detectors.OpaqueBoolean.handler
    |> compose Detectors.LogicalControlFlow.handler
    |> compose (
        Detectors.MatchOpportunity.handler (
            thresholds.MatchOpportunity
            |> Option.defaultValue Detectors.MatchOpportunity.defaultThresholds
        )
    )
    |> compose Detectors.Inversion.handler
    |> compose Detectors.ParameterCount.handler
    |> compose (
        Detectors.MagicString.handler (
            thresholds.MagicString
            |> Option.defaultValue Detectors.MagicString.defaultOptions
        )
    )
    |> compose (
        Detectors.MagicNumber.handler (
            thresholds.MagicNumber
            |> Option.defaultValue Detectors.MagicNumber.defaultOptions
        )
    )
