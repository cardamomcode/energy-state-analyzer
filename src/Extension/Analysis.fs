module Energy.Extension.Analysis

open Fable.Core.JS

open Energy.Core.Analyze
open Energy.Core.Esaignore
open Energy.Core.Position
open Energy.Core.TreeSitter
open Energy.Extension.Configuration
open Energy.Extension.Vscode.Document
open Energy.Extension.Vscode.Host
open Energy.Extension.Vscode.Workspace

type LoadedLanguage =
    { Adapter: Energy.Core.LanguageAdapter.LanguageAdapter
      Parser: Parser }

let private logError (message: string) (analysisError: AnalysisError) : unit =
    console.error (message, analysisErrorMessage analysisError)

// A standalone document has no workspace root from which an .esaignore can be read, so it is
// intentionally never ignored. includeFixtures is an editor-only override; scans always honor it.
let isDocumentIgnored (document: obj) =
    match workspaceFolderFor workspace (documentUri document) with
    | null -> false
    | folder when includeFixtures () -> false
    | folder ->
        let rootDir = workspaceFolderUri folder |> uriFsPath
        // decision: fully qualified instead of an `open` — this file sits at the coherence
        // detector's 10-import threshold, and Paths is needed at exactly this one call site.
        loadIgnorePatterns rootDir
        |> isIgnored (Energy.Core.Paths.Path(documentFileName document)) (Energy.Core.Paths.Path rootDir)

let private parseDocument fileName parser source =
    try
        parse parser source |> rootNode |> Ok
    with error ->
        Error(ParseFailed(fileName, string error))

let private analyze loaded document =
    let source = documentText document
    let fileName = documentFileName document

    // decision: presentation consumes the Core result directly. Optional Python type-information
    // logging is not part of analysis, so it must never turn valid findings into an empty editor.
    parseDocument fileName loaded.Parser source
    |> Result.map (fun root ->
        { Source = source
          Tree = root
          Language = loaded.Adapter
          FileName = fileName }
        |> analyzeWith (readAnalyzeThresholds ()))

// decision: handles typed boundary failures at the document boundary to retain the extension's
// existing UX: report the error but clear decorations rather than leave stale findings visible.
let analyzeDocument loaded document =
    match analyze loaded document with
    | Ok result -> result.Violations
    | Error analysisError ->
        logError "Error analyzing document:" analysisError
        []
