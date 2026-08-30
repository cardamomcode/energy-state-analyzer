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

let loadParser (adapter: LanguageAdapter) : Task<Parser> =
    task {
        match parserCache.TryGetValue adapter.Id with
        | true, parser -> return parser
        | false, _ ->
            do! init parserCtor
            let! grammar = load languageCtor (grammarPath adapter.GrammarPath)
            let parser = makeParser parserCtor
            setLanguage parser grammar |> ignore
            parserCache.Add(adapter.Id, parser)
            return parser
    }

let analyzeFile (filePath: string) (sourceText: string) (thresholds: AnalyzeThresholds) =
    task {
        match resolveLanguageForFile filePath with
        | None -> return []
        | Some adapter ->
            let! parser = loadParser adapter
            let tree = parse parser sourceText |> rootNode
            return analyzeSourceWith thresholds sourceText tree adapter filePath
    }

// decision: analyzes files sequentially — grammar loads are cached and report rows retain source
// order without relying on Fable Task.WhenAll support.
let rec analyzeFiles (files: string list) (thresholds: AnalyzeThresholds) =
    task {
        match files with
        | [] -> return []
        | file :: rest ->
            let! violations = analyzeFile file (readFileSync file "utf8") thresholds
            let! remaining = analyzeFiles rest thresholds

            return
                { FilePath = relativePath (cwd ()) file
                  Violations = violations }
                :: remaining
    }
