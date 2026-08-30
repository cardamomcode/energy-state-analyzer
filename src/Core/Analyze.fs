module Energy.Core.Analyze

open Fable.Core
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context

// The detector pipeline (port of src/core/analyze.ts).
//
// decision: `allDetectors` is the single place new detectors are registered — adding one means
// appending its `detector` value here, which is exactly the "adding a new detector" rule in
// AGENTS.md. Each detector owns its own threshold record and default (see Detectors/*), so no
// central Thresholds record lives on the context; runPipeline just composes each detector over the
// shared AnalysisContext.

let allDetectors: Detector list =
    [ Detectors.Nesting.detector
      Detectors.Cyclomatic.detector
      Detectors.Cognitive.detector
      Detectors.Coherence.detector
      Detectors.MagicNumber.detector
      Detectors.MagicString.detector
      Detectors.ParameterCount.detector
      Detectors.Inversion.detector
      Detectors.MatchOpportunity.detector ]

let runPipeline (ctx: AnalysisContext) : EnergyViolation list =
    allDetectors |> List.collect (fun d -> d.Run ctx)

// The pipeline entry point used by the CLI and tests — mirrors the TS `analyzeSource` signature.
let analyzeSource
    (sourceText: string)
    (tree: Node)
    (language: LanguageAdapter)
    (fileName: string)
    : EnergyViolation list =
    let ctx =
        { Source = sourceText
          Tree = tree
          Positions = createPositionLookup sourceText
          Language = language
          FileName = fileName }

    runPipeline ctx
