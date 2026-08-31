module Energy.Core.DetectorPipelineComplexity

open Energy.Core.AnalysisPipeline
open Energy.Core.DetectorPipeline

let handler (thresholds: AnalyzeThresholds) : AnalysisHandler =
    Detectors.Coherence.handler (
        thresholds.Coherence
        |> Option.defaultValue Detectors.Coherence.defaultCoherenceThresholds
    )
    |> compose (
        Detectors.Cognitive.handler (
            thresholds.Cognitive
            |> Option.defaultValue Detectors.Cognitive.defaultCognitiveThresholds
        )
    )
    |> compose (
        Detectors.Cyclomatic.handler (
            thresholds.Cyclomatic
            |> Option.defaultValue Detectors.Cyclomatic.defaultCyclomaticThresholds
        )
    )
    |> compose (
        Detectors.Nesting.handler (
            thresholds.Nesting
            |> Option.defaultValue Detectors.Nesting.defaultNestingThresholds
        )
    )
