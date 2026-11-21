// # DetectorPipeline
//
// The detector registry composes independent context transformations in a fixed order. Every stage has its
// own enable flag, and Context.addViolations accumulates findings in reverse order. Analyze.fs restores
// that order and performs suppression only after all enabled stages have run; adding a stage here affects
// both hosts.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.DetectorPipeline

open Energy.Core.Context

/// Run a detector stage only when its flag is enabled, threading the shared immutable context.
///
/// decision: each detector transforms one immutable context, keeping the pipeline's data flow
/// explicit instead of encoding it in continuation handlers.
/// invariant: violations accumulate in reverse order until Analyze reverses them before suppression.
///
/// A disabled detector is skipped entirely (its stage is a no-op), so enabling or disabling one has
/// no effect on the others — the flags are independent toggles, not a master switch.
let private runWhen
    (enabled: bool)
    (stage: AnalysisContext -> AnalysisContext)
    (ctx: AnalysisContext)
    : AnalysisContext =
    if enabled then stage ctx else ctx

/// Run the registered detector stages in their declared order, skipping each disabled option independently.
/// Findings stay reversed until Analyze.runPipeline restores order and applies source suppressions.
let detectorPipeline (ctx: AnalysisContext) : AnalysisContext =
    ctx
    |> runWhen ctx.Options.PrimitiveObsession.Enabled Detectors.PrimitiveObsession.detector.Run
    |> runWhen ctx.Options.ParseDontValidate.Enabled Detectors.ParseDontValidate.detector.Run
    |> runWhen ctx.Options.OpaqueBoolean.Enabled Detectors.OpaqueBoolean.detector.Run
    |> runWhen ctx.Options.LogicalControlFlow.Enabled Detectors.LogicalControlFlow.detector.Run
    |> runWhen ctx.Options.MatchOpportunity.Enabled Detectors.MatchOpportunity.detector.Run
    |> runWhen ctx.Options.Inversion.Enabled Detectors.Inversion.detector.Run
    |> runWhen ctx.Options.ErrorShadowing.Enabled Detectors.ErrorShadowing.detector.Run
    |> runWhen ctx.Options.ParameterCount.Enabled Detectors.ParameterCount.detector.Run
    |> runWhen ctx.Options.MagicString.Enabled Detectors.MagicString.detector.Run
    |> runWhen ctx.Options.MagicNumber.Enabled Detectors.MagicNumber.detector.Run
    |> runWhen ctx.Options.Coherence.Enabled Detectors.Coherence.detector.Run
    |> runWhen ctx.Options.Cognitive.Enabled Detectors.Cognitive.detector.Run
    |> runWhen ctx.Options.Cyclomatic.Enabled Detectors.Cyclomatic.detector.Run
    |> runWhen ctx.Options.Nesting.Enabled Detectors.Nesting.detector.Run
