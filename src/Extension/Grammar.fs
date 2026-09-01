module Energy.Extension.Grammar

open System.Collections.Generic
open System.Threading.Tasks

open Fable.Core
open Fable.Core.JS

open Energy.Core.Analyze
open Energy.Core.Paths
open Energy.Core.TreeSitter
open Energy.Extension.Analysis
open Energy.Languages.Registry

type GrammarContext =
    { ExtensionPath: string
      LoadedLanguages: Dictionary<string, LoadedLanguage>
      InFlightLoads: Dictionary<string, Task<Result<LoadedLanguage, AnalysisError>>> }

// decision: path arguments and the Path result are Core.Paths.Path (erased to their backing
// strings) so the message/path string pair can no longer be transposed and the result flows
// straight into load/logPath without a string round-trip.
[<Import("join", "node:path")>]
let private joinPath (left: Path) (right: Path) : Path = nativeOnly

let private logPath (message: string) (path: Path) : unit = console.log (message, path)

let private logSuccess (message: string) : unit = console.log (message)

let initializeParser () = init parserCtor

// decision: shares the pending task as well as completed parsers; an edit event that arrives
// while a grammar is loading cannot start another WASM load for the same language.
let getOrLoadLanguage
    (languageId: string)
    (context: GrammarContext)
    : Task<Result<LoadedLanguage option, AnalysisError>> =
    match context.LoadedLanguages.TryGetValue languageId with
    | true, language -> Task.FromResult(Ok(Some language))
    | false, _ ->
        match tryFind languageId with
        | None -> Task.FromResult(Ok None)
        | Some adapter ->
            let pending =
                match context.InFlightLoads.TryGetValue languageId with
                | true, existing -> existing
                | false, _ ->
                    task {
                        try
                            let grammarPath = joinPath (Path context.ExtensionPath) (Path adapter.GrammarPath)
                            logPath ("📁 Loading " + adapter.Id + " grammar:") grammarPath
                            let! grammar = load languageCtor grammarPath
                            let parser = makeParser parserCtor
                            setLanguage parser grammar |> ignore
                            let loaded = { Adapter = adapter; Parser = parser }
                            context.LoadedLanguages.Add(adapter.Id, loaded)
                            logSuccess ("✅ " + adapter.Id + " grammar loaded successfully")
                            return Ok loaded
                        with error ->
                            return Error(GrammarLoadFailed(adapter.Id, string error))
                    }

            if not (context.InFlightLoads.ContainsKey languageId) then
                context.InFlightLoads.Add(languageId, pending)

            task {
                let! loaded = pending
                return loaded |> Result.map Some
            }
