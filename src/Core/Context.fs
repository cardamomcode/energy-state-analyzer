module Energy.Core.Context

open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter

type NestingThresholds =
    { MediumThreshold: int
      HighThreshold: int }

type CyclomaticThresholds =
    { MediumThreshold: int
      HighThreshold: int }

type CognitiveThresholds =
    { MediumThreshold: int
      HighThreshold: int }

type CoherenceThresholds =
    { LargeFunctionLines: int
      MaxLargeFunctions: int
      SingleDomainNameShare: float
      MaxTypeDiversityRatio: float
      MinTypedCoverage: float }

type MatchOpportunityThresholds = { MinBranches: int }

type MagicNumberOptions =
    { Enabled: bool
      Allowlist: float list
      IncludeTestFiles: bool }

type MagicStringOptions =
    { Enabled: bool
      MinDuplicates: int
      Allowlist: string list
      IncludeTestFiles: bool }

/// Host-independent settings supplied once per analysis request.
type AnalyzeOptions =
    { Nesting: NestingThresholds
      Cyclomatic: CyclomaticThresholds
      Cognitive: CognitiveThresholds
      Coherence: CoherenceThresholds
      MatchOpportunity: MatchOpportunityThresholds
      MagicNumber: MagicNumberOptions
      MagicString: MagicStringOptions }

// decision: default nesting thresholds 3/5 mark the point where active conditions strain working memory.
// decision: default cyclomatic thresholds 10/15 distinguish many paths from urgent extraction work.
// decision: default cognitive thresholds 15/25 align the nesting-weighted metric with SonarSource defaults.
// decision: coherence uses large-function count rather than raw function count because F# modules often have many small functions.
// decision: magic-detector test files stay exempt by default because their literals are usually intentional.
let defaultAnalyzeOptions =
    { Nesting =
        { MediumThreshold = 3
          HighThreshold = 5 }
      Cyclomatic =
        { MediumThreshold = 10
          HighThreshold = 15 }
      Cognitive =
        { MediumThreshold = 15
          HighThreshold = 25 }
      Coherence =
        { LargeFunctionLines = 20
          MaxLargeFunctions = 5
          SingleDomainNameShare = 0.7
          MaxTypeDiversityRatio = 0.4
          MinTypedCoverage = 0.5 }
      MatchOpportunity = { MinBranches = 3 }
      MagicNumber =
        { Enabled = true
          Allowlist = [ 0.0; 1.0; -1.0; 2.0 ]
          IncludeTestFiles = false }
      MagicString =
        { Enabled = true
          MinDuplicates = 2
          Allowlist = [ ""; "utf-8"; "__main__" ]
          IncludeTestFiles = false } }

// The shared immutable analysis context + the detector abstraction (port of the parameter
// repeated analysis parameters in the legacy implementation).
//
// decision: one immutable context replaces the repeated (tree, positions, language, fileName)
// quadruple — which is exactly the swap-risk shape this repo's own primitive-obsession detector
// flags. It carries every input that remains stable during one analysis, including the selected
// host-independent options, so detectors can uniformly transform `AnalysisContext -> AnalysisContext`.

type AnalysisContext =
    { Source: string
      // Root node of the already-parsed tree — the pipeline is synchronous, so no task { } here.
      Tree: Node
      // Offset->line/column lookup derived from Source; detectors map node.startIndex through it.
      Positions: PositionLookup
      Language: LanguageAdapter
      FileName: string
      Options: AnalyzeOptions
      // Findings accumulate in reverse detector/source order so each detector can prepend without
      // repeatedly copying the findings already produced by earlier stages.
      Violations: EnergyViolation list }

/// Add findings from one detector while retaining linear accumulation across the whole pipeline.
let addViolations (findings: EnergyViolation list) (ctx: AnalysisContext) : AnalysisContext =
    { ctx with
        Violations = List.rev findings @ ctx.Violations }

// A named detector stage over the shared context. Each stage receives all document facts and the
// findings emitted by preceding stages, then returns the updated context for the next stage.
type Detector =
    { Name: string
      Run: AnalysisContext -> AnalysisContext }
