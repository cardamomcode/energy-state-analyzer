module Energy.Extension.Analysis

open Fable.Core
open Fable.Core.JS

open Energy.Core.Analyze
open Energy.Core.Esaignore
open Energy.Core.PythonTypeInfoExtraction
open Energy.Core.Position
open Energy.Core.TreeSitter
open Energy.Extension.Configuration
open Energy.Extension.VscodeDocument
open Energy.Extension.VscodeHost
open Energy.Extension.VscodeWorkspace
open Energy.Languages.Registry

type LoadedLanguage =
    { Adapter: Energy.Core.LanguageAdapter.LanguageAdapter
      Parser: Parser }

let private logError (message: string) (error: obj) : unit = console.error(message, error)

let private logValue (message: string) (value: obj) : unit = console.log(message, value)

// A standalone document has no workspace root from which an .esaignore can be read, so it is
// intentionally never ignored. includeFixtures is an editor-only override; scans always honor it.
let isDocumentIgnored (document: obj) =
    match workspaceFolderFor workspace (documentUri document) with
    | null -> false
    | folder when includeFixtures () -> false
    | folder ->
        let rootDir = workspaceFolderUri folder |> uriFsPath
        loadIgnorePatterns rootDir |> isIgnored (documentFileName document) rootDir

let private analyze loaded document =
    try
        let source = documentText document
        let tree = parse loaded.Parser source
        let root = rootNode tree

        let violations =
            analyzeSourceWith (readAnalyzeThresholds ()) source root loaded.Adapter (documentFileName document)

        if loaded.Adapter.Id = "python" then
            extractTypeInformation tree (createPositionLookup source)
            |> box
            |> logValue "🔍 Found types:"

        Ok violations
    with error ->
        Error error

// decision: catches parsing/analyzer failures at the document boundary to retain the extension's
// existing UX: report the error but clear decorations rather than leave stale findings visible.
let analyzeDocument loaded document =
    match analyze loaded document with
    | Ok violations -> violations
    | Error error ->
        logError "Error analyzing document:" (box error)
        []
