module Energy.Core.DetectorPipeline

open Energy.Core.AnalysisPipeline

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

// decision: detector handlers compose before execution, like HTTP handlers; the AnalysisContext
// reaches the resulting function only at runDefault, which invokes the complete chain once.
// invariant: detector results retain declaration order before suppression sees them.
let defaultHandler: AnalysisHandler =
    Detectors.PrimitiveObsession.handler
    |> compose Detectors.OpaqueBoolean.handler
    |> compose Detectors.LogicalControlFlow.handler
    |> compose Detectors.MatchOpportunity.defaultHandler
    |> compose Detectors.Inversion.handler
    |> compose Detectors.ParameterCount.handler
    |> compose Detectors.MagicString.defaultHandler
    |> compose Detectors.MagicNumber.defaultHandler
    |> compose Detectors.Coherence.defaultHandler
    |> compose Detectors.Cognitive.defaultHandler
    |> compose Detectors.Cyclomatic.defaultHandler
    |> compose Detectors.Nesting.defaultHandler

let defaultPipeline: AnalysisFunc = defaultHandler empty

let runDefault ctx = defaultPipeline ctx
