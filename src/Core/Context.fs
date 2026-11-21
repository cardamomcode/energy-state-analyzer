// # Context
//
// A document analysis shares immutable source, syntax, positions, language policy, and options across
// detector stages. Each stage returns a new context with its findings prepended in reverse order; it does
// not mutate the source or own the tree. Analyze.fs is responsible for final ordering and suppression.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.Context

open Energy.Core.Violation
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Config

/// The shared immutable analysis context + the detector abstraction (port of the parameter
/// repeated analysis parameters in the legacy implementation).
///
/// decision: one immutable context replaces the repeated (tree, positions, language, fileName)
/// quadruple — which is exactly the swap-risk shape this repo's own primitive-obsession detector
/// flags. It carries every input that remains stable during one analysis, including the selected
/// host-independent options, so detectors can uniformly transform `AnalysisContext -> AnalysisContext`.

type AnalysisContext =
    {
        Source: string
        // Root node of the already-parsed tree — the pipeline is synchronous, so no task { } here.
        Tree: Node
        // Offset->line/column lookup derived from Source; detectors map node.startIndex through it.
        Positions: Position.PositionLookup
        Language: LanguageAdapter
        FileName: string
        Options: AnalyzeOptions
        // Findings accumulate in reverse detector/source order so each detector can prepend without
        // repeatedly copying the findings already produced by earlier stages.
        Violations: EnergyViolation list
    }

/// Add findings from one detector while retaining linear accumulation across the whole pipeline.
/// findings models this stage's output; before and after model the context's reversed finding lists.
///
/// lean: `def AddViolationsContract (findings before after : List α) : Prop := after.reverse = before.reverse ++ findings`
let addViolations (findings: EnergyViolation list) (ctx: AnalysisContext) : AnalysisContext =
    { ctx with
        Violations = List.rev findings @ ctx.Violations
    }

/// A named detector stage over the shared context. Each stage receives all document facts and the
/// findings emitted by preceding stages, then returns the updated context for the next stage.
type Detector =
    {
        Name: string
        Run: AnalysisContext -> AnalysisContext
    }
