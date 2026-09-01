module Energy.CliRuntime

open System.Collections.Generic
open System.Threading.Tasks

open Energy.CliNode
open Energy.Core.Analyze
open Energy.Core.LanguageAdapter
open Energy.Core.Report
open Energy.Core.TreeSitter
open Energy.Languages.Registry

// Parser instances are cached by adapter so every scan/diff invocation pays each grammar's WASM
// load at most once while preserving deterministic sequential report ordering.

let private parserCache = Dictionary<string, Parser>()

let private grammarPath relative =
    joinPath (joinPath bundleDirectory "..") relative

let loadParser (adapter: LanguageAdapter) : Task<Result<Parser, AnalysisError>> =
    task {
        match parserCache.TryGetValue adapter.Id with
        | true, parser -> return Ok parser
        | false, _ ->
            try
                do! init parserCtor
                let! grammar = load languageCtor (grammarPath adapter.GrammarPath)
                let parser = makeParser parserCtor
                setLanguage parser grammar |> ignore
                parserCache.Add(adapter.Id, parser)
                return Ok parser
            with error ->
                return Error(GrammarLoadFailed(adapter.Id, string error))
    }

let private parseSource (filePath: string) (parser: Parser) (sourceText: string) =
    try
        parse parser sourceText |> rootNode |> Ok
    with error ->
        Error(ParseFailed(filePath, string error))

let analyzeFile (filePath: string) (sourceText: string) (thresholds: AnalyzeThresholds) =
    task {
        match resolveLanguageForFile filePath with
        | None -> return Error(UnsupportedLanguage filePath)
        | Some adapter ->
            let! parserResult = loadParser adapter

            return
                parserResult
                |> Result.bind (fun parser -> parseSource filePath parser sourceText)
                |> Result.map (fun tree ->
                    { Source = sourceText
                      Tree = tree
                      Language = adapter
                      FileName = filePath }
                    |> analyzeWith thresholds
                    |> _.Violations)
    }

let private readSource (filePath: string) =
    try
        readFileSync filePath "utf8" |> Ok
    with error ->
        Error(SourceReadFailed(filePath, string error))

let analyzePath (filePath: string) (thresholds: AnalyzeThresholds) =
    task {
        match readSource filePath with
        | Error error -> return Error error
        | Ok sourceText -> return! analyzeFile filePath sourceText thresholds
    }

// decision: analyzes files sequentially — grammar loads are cached and report rows retain source
// order without relying on Fable Task.WhenAll support.
let rec analyzeFiles (files: string list) (thresholds: AnalyzeThresholds) =
    task {
        match files with
        | [] -> return Ok []
        | file :: rest ->
            let! analysis = analyzePath file thresholds

            match analysis with
            | Error error -> return Error error
            | Ok violations ->
                let! remaining = analyzeFiles rest thresholds

                return
                    remaining
                    |> Result.map (fun results ->
                        { FilePath = relativePath (cwd ()) file
                          Violations = violations }
                        :: results)
    }
