module Energy.Core.DetectorPipeline

open Energy.Core.Context

// decision: each detector transforms one immutable context, keeping the pipeline's data flow
// explicit instead of encoding it in continuation handlers.
// invariant: violations accumulate in reverse order until Analyze reverses them before suppression.
//
// A disabled detector is skipped entirely (its stage is a no-op), so enabling or disabling one has
// no effect on the others — the flags are independent toggles, not a master switch.
let private runWhen
    (enabled: bool)
    (stage: AnalysisContext -> AnalysisContext)
    (ctx: AnalysisContext)
    : AnalysisContext =
    if enabled then stage ctx else ctx

let detectorPipeline (ctx: AnalysisContext) : AnalysisContext =
    ctx
    |> runWhen ctx.Options.PrimitiveObsession.Enabled Detectors.PrimitiveObsession.detector.Run
    |> runWhen ctx.Options.OpaqueBoolean.Enabled Detectors.OpaqueBoolean.detector.Run
    |> runWhen ctx.Options.LogicalControlFlow.Enabled Detectors.LogicalControlFlow.detector.Run
    |> runWhen ctx.Options.MatchOpportunity.Enabled Detectors.MatchOpportunity.detector.Run
    |> runWhen ctx.Options.Inversion.Enabled Detectors.Inversion.detector.Run
    |> runWhen ctx.Options.ParameterCount.Enabled Detectors.ParameterCount.detector.Run
    |> runWhen ctx.Options.MagicString.Enabled Detectors.MagicString.detector.Run
    |> runWhen ctx.Options.MagicNumber.Enabled Detectors.MagicNumber.detector.Run
    |> runWhen ctx.Options.Coherence.Enabled Detectors.Coherence.detector.Run
    |> runWhen ctx.Options.Cognitive.Enabled Detectors.Cognitive.detector.Run
    |> runWhen ctx.Options.Cyclomatic.Enabled Detectors.Cyclomatic.detector.Run
    |> runWhen ctx.Options.Nesting.Enabled Detectors.Nesting.detector.Run
