module Energy.CliRuntime

open System.Collections.Generic
open System.Threading.Tasks

open Energy.CliNode
open Energy.Core.Analyze
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Core.LanguageAdapter
open Energy.Core.Report
open Energy.Core.TreeSitter
open Energy.Languages.Registry

/// Parser instances are cached by adapter so every scan/diff invocation pays each grammar's WASM
/// load at most once while preserving deterministic sequential report ordering.

let private parserCache = Dictionary<string, Parser>()

let private grammarPath relative =
    joinPath (joinPath bundleDirectory (Path "..")) (Path relative)

/// Initialize and load a grammar into a parser, mapping failures to GrammarLoadFailed.
///
/// decision: contains only the tree-sitter boundary whose exceptions have a user-actionable
/// GrammarLoadFailed representation; cache mutation remains outside so programming failures surface.
let private createParser (adapter: LanguageAdapter) : Task<Result<Parser, AnalysisError>> =
    task {
        try
            do! init ()
            let! grammar = load languageCtor (grammarPath adapter.GrammarPath)
            let parser = makeParser parserCtor
            setLanguage parser grammar |> ignore
            return Ok parser
        with error ->
            return Error(GrammarLoadFailed(adapter.Id, string<exn> error))
    }

let loadParser (adapter: LanguageAdapter) : Task<Result<Parser, AnalysisError>> =
    task {
        match parserCache.TryGetValue adapter.Id with
        | true, parser -> return Ok parser
        | false, _ ->
            let! parserResult = createParser adapter

            match parserResult with
            | Error error -> return Error error
            | Ok parser ->
                parserCache.Add(adapter.Id, parser)
                return Ok parser
    }

/// Resolve, parse, and analyze one file's source as a task returning its violations.
///
/// decision: `filePath` is a Core.Paths.Path destructured to its backing string, while
/// `sourceText` stays a raw string (it feeds AnalysisInput.Source) — the distinct types remove
/// the swap risk the string/string pair had.
let analyzeFile (Path filePath) (sourceText: string) (thresholds: AnalyzeThresholds) =
    task {
        match resolveLanguageForFile filePath with
        | None -> return Error(UnsupportedLanguage filePath)
        | Some adapter ->
            let! parserResult = loadParser adapter

            return
                parserResult
                |> Result.map (fun parser ->
                    withParsedTree parser sourceText (fun tree ->
                        { Source = sourceText
                          Tree = tree
                          Language = adapter
                          FileName = filePath }
                        |> analyzeWith thresholds
                        |> _.Violations))
    }

/// Read source safely so missing or unreadable files produce a typed boundary error.
let private readSource (filePath: Path) : Result<string, AnalysisError> =
    // decision: use the safe Node binding so a missing/unreadable file becomes an Error payload
    // instead of a thrown exception — no try/with here for the error-shadowing detector to flag.
    match Energy.Core.NodeInterop.readFileSyncSafe filePath (Encoding "utf8") with
    | Ok sourceText -> Ok sourceText
    | Error message ->
        // decision: the error payload is the string edge of this module — unwrap once here for the
        // message rather than threading a Path through AnalysisError.
        let (Path file) = filePath
        Error(SourceReadFailed(file, message))

let analyzePath (filePath: Path) (thresholds: AnalyzeThresholds) =
    task {
        match readSource filePath with
        | Error error -> return Error error
        | Ok sourceText -> return! analyzeFile filePath sourceText thresholds
    }

/// Analyze a list of files sequentially, preserving source order in the report rows.
///
/// decision: analyzes files sequentially — grammar loads are cached and report rows retain source
/// order without relying on Fable Task.WhenAll support.
let rec analyzeFiles (files: Path list) (thresholds: AnalyzeThresholds) =
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
                        { FilePath = relativePath (Path(cwd ())) file
                          Violations = violations }
                        :: results)
    }
