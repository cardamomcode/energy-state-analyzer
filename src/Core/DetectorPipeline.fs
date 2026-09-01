module Energy.Core.DetectorPipeline

open Energy.Core.Context

// decision: each detector transforms one immutable context, keeping the pipeline's data flow
// explicit instead of encoding it in continuation handlers.
// invariant: violations accumulate in reverse order until Analyze reverses them before suppression.
let runDefault (ctx: AnalysisContext) : AnalysisContext =
    ctx
    |> Detectors.PrimitiveObsession.detector.Run
    |> Detectors.OpaqueBoolean.detector.Run
    |> Detectors.LogicalControlFlow.detector.Run
    |> Detectors.MatchOpportunity.detector.Run
    |> Detectors.Inversion.detector.Run
    |> Detectors.ParameterCount.detector.Run
    |> Detectors.MagicString.detector.Run
    |> Detectors.MagicNumber.detector.Run
    |> Detectors.Coherence.detector.Run
    |> Detectors.Cognitive.detector.Run
    |> Detectors.Cyclomatic.detector.Run
    |> Detectors.Nesting.detector.Run
