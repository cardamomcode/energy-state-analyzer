module Energy.Extension.Grammar

open System.Collections.Generic
open System.Threading.Tasks

open Fable.Core
open Fable.Core.JS

open Energy.Core.TreeSitter
open Energy.Extension.Analysis
open Energy.Languages.Registry

type GrammarContext =
    { ExtensionPath: string
      LoadedLanguages: Dictionary<string, LoadedLanguage>
      InFlightLoads: Dictionary<string, Task<LoadedLanguage>> }

[<Import("join", "node:path")>]
let private joinPath (left: string) (right: string) : string = nativeOnly

let private logPath (message: string) (path: string) : unit = console.log(message, path)

let private logSuccess (message: string) : unit = console.log(message)

let initializeParser () = init parserCtor

// decision: shares the pending task as well as completed parsers; an edit event that arrives
// while a grammar is loading cannot start another WASM load for the same language.
let getOrLoadLanguage (languageId: string) (context: GrammarContext) : Task<LoadedLanguage option> =
    match context.LoadedLanguages.TryGetValue languageId with
    | true, language -> Task.FromResult(Some language)
    | false, _ ->
        match tryFind languageId with
        | None -> Task.FromResult None
        | Some adapter ->
            let pending =
                match context.InFlightLoads.TryGetValue languageId with
                | true, existing -> existing
                | false, _ ->
                    task {
                        let grammarPath = joinPath context.ExtensionPath adapter.GrammarPath
                        logPath ("📁 Loading " + adapter.Id + " grammar:") grammarPath
                        let! grammar = load languageCtor grammarPath
                        let parser = makeParser parserCtor
                        setLanguage parser grammar |> ignore
                        let loaded = { Adapter = adapter; Parser = parser }
                        context.LoadedLanguages.Add(adapter.Id, loaded)
                        logSuccess ("✅ " + adapter.Id + " grammar loaded successfully")
                        return loaded
                    }

            if not (context.InFlightLoads.ContainsKey languageId) then
                context.InFlightLoads.Add(languageId, pending)

            task {
                let! loaded = pending
                return Some loaded
            }
