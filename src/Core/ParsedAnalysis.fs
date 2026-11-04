module Energy.Core.ParsedAnalysis

open Fable.Core

open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Core.LanguageAdapter
open Energy.Core.Paths
open Energy.Core.TreeSitter

/// Parse source, run the synchronous detector pipeline, and release the native tree.
///
/// invariant: parsing and detector failures return no partial findings, and the tree is released
/// after either a successful analysis or a detector failure.
let analyzeParsedSource
    (parser: Parser)
    (language: LanguageAdapter)
    (Path fileName)
    (source: string)
    (options: AnalyzeOptions)
    : Result<AnalysisResult, AnalysisError> =
    try
        withParsedTree parser source (fun tree ->
            try
                {
                    Source = source
                    Tree = tree
                    Language = language
                    FileName = fileName
                }
                |> analyzeWith options
                |> Ok
            with error ->
                Error(AnalysisFailed(fileName, string<exn> error)))
    with error ->
        Error(ParseFailed(fileName, string<exn> error))
