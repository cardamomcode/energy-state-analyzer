module Energy.Extension.Analysis

open Fable.Core.JS

open Energy.Core.Analyze
open Energy.Core.Esaignore
open Energy.Core.PythonTypeInfoExtraction
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

let private logValue (message: string) (value: obj) : unit = console.log (message, value)

// A standalone document has no workspace root from which an .esaignore can be read, so it is
// intentionally never ignored. includeFixtures is an editor-only override; scans always honor it.
let isDocumentIgnored (document: obj) =
    match workspaceFolderFor workspace (documentUri document) with
    | null -> false
    | folder when includeFixtures () -> false
    | folder ->
        let rootDir = workspaceFolderUri folder |> uriFsPath
        loadIgnorePatterns rootDir |> isIgnored (documentFileName document) rootDir

let private parseDocument fileName parser source =
    try
        parse parser source |> rootNode |> Ok
    with error ->
        Error(ParseFailed(fileName, string error))

let private logPythonTypeInformation fileName source (adapter: Energy.Core.LanguageAdapter.LanguageAdapter) tree =
    if adapter.Id <> "python" then
        Ok()
    else
        try
            extractTypeInformation tree (createPositionLookup source)
            |> box
            |> logValue "🔍 Found types:"

            Ok()
        with error ->
            Error(AnalysisFailed(fileName, string error))

let private analyze loaded document =
    let source = documentText document
    let fileName = documentFileName document

    parseDocument fileName loaded.Parser source
    |> Result.bind (fun root ->
        let result =
            { Source = source
              Tree = root
              Language = loaded.Adapter
              FileName = fileName }
            |> analyzeWith (readAnalyzeThresholds ())

        logPythonTypeInformation fileName source loaded.Adapter root
        |> Result.map (fun () -> result))

// decision: handles typed boundary failures at the document boundary to retain the extension's
// existing UX: report the error but clear decorations rather than leave stale findings visible.
let analyzeDocument loaded document =
    match analyze loaded document with
    | Ok result -> result.Violations
    | Error analysisError ->
        logError "Error analyzing document:" analysisError
        []
