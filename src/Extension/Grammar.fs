// # Grammar
//
// Each activation owns completed language/parser pairs and in-flight grammar tasks. Concurrent requests
// share pending work; success populates the completed cache and failure remains retryable once its pending
// task is removed. Unknown language IDs return a successful absence. Runtime initialization is shared
// through Core/TreeSitter.fs.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Extension.Grammar

open System.Collections.Generic
open System.Threading.Tasks

open Fable.Core.JS

open Energy.Core.Analyze
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Core.TreeSitter
open Energy.Extension.LoadedLanguage
open Energy.Languages.Registry

/// Activation-owned grammar state: package path, completed parsers, and shared pending load tasks keyed by
/// language ID. Failed loads leave no completed entry and can be retried.
type GrammarContext =
    {
        ExtensionPath: string
        LoadedLanguages: Dictionary<string, LoadedLanguage>
        InFlightLoads: Dictionary<string, Task<Result<LoadedLanguage, AnalysisError>>>
    }

/// Log the packaged grammar path being loaded to aid diagnosis of missing WASM assets.
let private logPath (message: string) (path: Path) : unit = console.log (message, path)

/// Log successful grammar initialization after the completed parser has entered the cache.
let private logSuccess (message: string) : unit = console.log (message)

/// Await the shared parser runtime before activating editor analysis.
let initializeParser () = init ()

/// Return the loaded grammar for a language, sharing an in-flight load instead of starting another.
///
/// decision: shares the pending task as well as completed parsers; an edit event that arrives
/// while a grammar is loading cannot start another WASM load for the same language.
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
                            return Error(GrammarLoadFailed(adapter.Id, string<exn> error))
                    }

            if not (context.InFlightLoads.ContainsKey languageId) then
                context.InFlightLoads.Add(languageId, pending)

            task {
                let! loaded = pending

                // decision: only the waiter for the current task removes it; a failed load must be
                // retryable on the next event without an older waiter removing a newer retry.
                match context.InFlightLoads.TryGetValue languageId with
                | true, current when current = pending -> context.InFlightLoads.Remove languageId |> ignore
                | _ -> ()

                return loaded |> Result.map Some
            }
