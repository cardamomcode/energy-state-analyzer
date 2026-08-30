module Energy.Core.Analyze

open Fable.Core
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context
open Energy.Core.Suppressions

type AnalyzeThresholds =
    { Nesting: Detectors.Nesting.NestingThresholds option
      Cyclomatic: Detectors.Cyclomatic.CyclomaticThresholds option
      Cognitive: Detectors.Cognitive.CognitiveThresholds option
      Coherence: Detectors.Coherence.CoherenceThresholds option
      MatchOpportunity: Detectors.MatchOpportunity.MatchOpportunityThresholds option
      MagicNumber: Detectors.MagicNumber.MagicNumberOptions option
      MagicString: Detectors.MagicString.MagicStringOptions option }

let defaultThresholds =
    { Nesting = None
      Cyclomatic = None
      Cognitive = None
      Coherence = None
      MatchOpportunity = None
      MagicNumber = None
      MagicString = None }

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
      Detectors.MatchOpportunity.detector
      Detectors.LogicalControlFlow.detector
      Detectors.OpaqueBoolean.detector
      Detectors.PrimitiveObsession.detector ]

let runPipeline (ctx: AnalysisContext) : EnergyViolation list =
    let result = allDetectors |> List.collect (fun d -> d.Run ctx)
    let suppressed = applySuppressions result ctx.Source
    suppressed.Violations @ suppressed.SuppressionNotes

// decision: keeps the threshold record at this composition boundary rather than Context — detector
// option types stay owned by their modules, avoiding a core-context dependency cycle while CLI and
// extension callers still share one configuration contract.
let runPipelineWith (thresholds: AnalyzeThresholds) (ctx: AnalysisContext) : EnergyViolation list =
    let results =
        [ Detectors.Nesting.analyzeNesting
              ctx
              (thresholds.Nesting
               |> Option.defaultValue Detectors.Nesting.defaultNestingThresholds)
          Detectors.Cyclomatic.analyzeFunctionComplexity
              ctx.Tree
              ctx.Positions
              ctx.Language
              (thresholds.Cyclomatic
               |> Option.defaultValue Detectors.Cyclomatic.defaultCyclomaticThresholds)
          Detectors.Cognitive.analyzeCognitiveComplexity
              ctx.Tree
              ctx.Positions
              ctx.Language
              (thresholds.Cognitive
               |> Option.defaultValue Detectors.Cognitive.defaultCognitiveThresholds)
          Detectors.Coherence.analyzeFileCoherence
              ctx.Tree
              ctx.FileName
              ctx.Language
              ctx.Positions
              (thresholds.Coherence
               |> Option.defaultValue Detectors.Coherence.defaultCoherenceThresholds)
          Detectors.MagicNumber.analyzeMagicNumbers
              ctx.Tree
              ctx.Positions
              ctx.Language
              ctx.FileName
              (thresholds.MagicNumber
               |> Option.defaultValue Detectors.MagicNumber.defaultOptions)
          Detectors.MagicString.analyzeMagicStrings
              ctx.Tree
              ctx.Positions
              ctx.Language
              (thresholds.MagicString
               |> Option.defaultValue Detectors.MagicString.defaultOptions)
          Detectors.ParameterCount.analyzeParameterCount ctx.Tree ctx.Positions ctx.Language
          Detectors.Inversion.analyzeInversionOpportunities ctx.Tree ctx.Positions ctx.Language
          Detectors.MatchOpportunity.analyzeMatchOpportunities
              ctx.Tree
              ctx.Positions
              ctx.Language
              (thresholds.MatchOpportunity
               |> Option.defaultValue Detectors.MatchOpportunity.defaultThresholds)
          Detectors.LogicalControlFlow.analyzeLogicalControlFlow ctx.Tree ctx.Positions ctx.Language
          Detectors.OpaqueBoolean.analyzeOpaqueBooleanLiteral ctx.Tree ctx.Positions ctx.Language
          Detectors.PrimitiveObsession.analyzePrimitiveObsession ctx.Tree ctx.Positions ctx.Language ]
        |> List.collect id

    let suppressed = applySuppressions results ctx.Source
    suppressed.Violations @ suppressed.SuppressionNotes

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

let analyzeSourceWith thresholds sourceText tree language fileName =
    let ctx =
        { Source = sourceText
          Tree = tree
          Positions = createPositionLookup sourceText
          Language = language
          FileName = fileName }

    runPipelineWith thresholds ctx
