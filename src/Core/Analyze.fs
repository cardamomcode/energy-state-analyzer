module Energy.Core.Analyze

open Fable.Core
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Context
open Energy.Core.DetectorPipeline
open Energy.Core.Suppressions

type AnalyzeThresholds = DetectorPipeline.AnalyzeThresholds

let defaultThresholds = DetectorPipeline.defaultThresholds

/// Everything the synchronous analyzer needs after a host has parsed a document.
///
/// The parser and file-system adapters deliberately stay outside this input: Core transforms
/// already-available source and syntax data without knowing whether it came from VS Code or Node.
type AnalysisInput =
    { Source: string
      Tree: Node
      Language: LanguageAdapter
      FileName: string }

/// The value produced by the host-independent analyzer pipeline.
type AnalysisResult = { Violations: EnergyViolation list }

/// Failures that can occur while a host prepares or executes an analysis request.
///
/// The synchronous Core transformations remain total for a valid parsed tree. These cases make
/// failures at the file-system and tree-sitter boundaries explicit to the CLI and extension.
///
/// decision: represents boundary failures as data so hosts can choose an appropriate UI or exit
/// code without relying on a catch-all exception handler.
/// invariant: a failed analysis never produces a partial violation list.
type AnalysisError =
    | UnsupportedLanguage of filePath: string
    | SourceReadFailed of filePath: string * message: string
    | GrammarLoadFailed of languageId: string * message: string
    | ParseFailed of filePath: string * message: string
    | AnalysisFailed of filePath: string * message: string

let analysisErrorMessage error =
    match error with
    | UnsupportedLanguage filePath -> "Unsupported file type: " + filePath
    | SourceReadFailed(filePath, message) -> "Could not read " + filePath + ": " + message
    | GrammarLoadFailed(languageId, message) -> "Could not load " + languageId + " grammar: " + message
    | ParseFailed(filePath, message) -> "Could not parse " + filePath + ": " + message
    | AnalysisFailed(filePath, message) -> "Could not analyze " + filePath + ": " + message

let private applySuppressionStage (ctx: AnalysisContext) (violations: EnergyViolation list) =
    let suppressed = applySuppressions violations ctx.Source
    suppressed.Violations @ suppressed.SuppressionNotes

let runPipeline (ctx: AnalysisContext) : EnergyViolation list =
    ctx |> runDefault |> applySuppressionStage ctx

let runPipelineWith (thresholds: AnalyzeThresholds) (ctx: AnalysisContext) : EnergyViolation list =
    ctx
    |> DetectorPipelineConfigured.runWith thresholds
    |> applySuppressionStage ctx

let private createContext (input: AnalysisInput) : AnalysisContext =
    { Source = input.Source
      Tree = input.Tree
      Positions = createPositionLookup input.Source
      Language = input.Language
      FileName = input.FileName }

let private toResult violations : AnalysisResult = { Violations = violations }

/// Analyze a parsed document with the default detector thresholds.
///
/// decision: exposes the analyzer as a data transformation pipeline, so parsing, error handling,
/// and presentation remain host adapters instead of becoming dependencies of Core.
/// invariant: detector ordering and suppression application remain stable across host adapters.
let analyze (input: AnalysisInput) : AnalysisResult =
    input |> createContext |> runPipeline |> toResult

/// Analyze a parsed document with caller-supplied detector thresholds.
let analyzeWith (thresholds: AnalyzeThresholds) (input: AnalysisInput) : AnalysisResult =
    input |> createContext |> runPipelineWith thresholds |> toResult
