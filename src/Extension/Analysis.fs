// # Analysis
//
// Editor analysis reads document text and resource-scoped configuration, applies workspace ignore policy,
// and delegates parsing to the shared Core boundary. Standalone documents have no workspace ignore file.
// Typed failures are logged and become empty findings so the composition root clears stale presentation;
// lower-level source APIs retain Result errors.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Extension.Analysis

open Fable.Core.JS

open Energy.Core.Analyze
open Energy.Core.Esaignore
open Energy.Core.ParsedAnalysis
open Energy.Core.TreeSitter
open Energy.Extension.Configuration
open Energy.Extension.LoadedLanguage
open Energy.Extension.Vscode.Document
open Energy.Extension.Vscode.Host
open Energy.Extension.Vscode.Workspace

/// Log a contextual message and the formatted typed analysis failure to the extension console.
let private logError (message: string) (analysisError: AnalysisError) : unit =
    console.error (message, analysisErrorMessage analysisError)

/// A standalone document has no workspace root from which an .esaignore can be read, so it is
/// intentionally never ignored. includeFixtures is an editor-only override; scans always honor it.
let isDocumentIgnored (document: obj) =
    match workspaceFolderFor workspace (documentUri document) with
    | null -> false
    | folder when includeFixtures (documentUri document) -> false
    | folder ->
        let rootDir = workspaceFolderUri folder |> uriFsPath
        // decision: fully qualified instead of an `open` — this file sits at the coherence
        // detector's 10-import threshold, and Paths is needed at exactly this one call site.
        loadIgnorePatterns rootDir
        |> isIgnored (Energy.Core.Paths.Path(documentFileName document)) (Energy.Core.Paths.Path rootDir)

/// Analyze source while keeping native syntax-tree ownership within this synchronous call.
let analyzeSourceWith thresholds loaded fileName source =
    // decision: both hosts use the same parser boundary; presentation receives only detached
    // findings, never live tree-sitter nodes whose tree has already been released.
    analyzeParsedSource loaded.Parser loaded.Adapter (Energy.Core.Paths.Path fileName) source thresholds

/// Read default workspace thresholds and analyze supplied source with a loaded parser. Configuration or
/// analysis failures remain Result errors for the caller.
let analyzeSource loaded fileName source =
    readAnalyzeThresholds ()
    |> Result.bind (fun thresholds -> analyzeSourceWith thresholds loaded fileName source)

/// Analyze an editor document with the configuration of its own workspace folder.
let private analyze loaded document =
    readAnalyzeThresholdsFor (documentUri document)
    |> Result.bind (fun thresholds ->
        analyzeSourceWith thresholds loaded (documentFileName document) (documentText document))

/// Analyze the active document, clearing decorations on a typed boundary failure.
///
/// decision: handles typed boundary failures at the document boundary to retain the extension's
/// existing UX: report the error but clear decorations rather than leave stale findings visible.
let analyzeDocument loaded document =
    match analyze loaded document with
    | Ok result -> result.Violations
    | Error analysisError ->
        logError "Error analyzing document:" analysisError
        []
