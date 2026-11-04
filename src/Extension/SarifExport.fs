module Energy.Extension.SarifExport

open System.Threading.Tasks

open Fable.Core
open Energy.Core.Analyze
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Extension.Analysis
open Energy.Extension.Grammar
open Energy.Extension.Vscode.Host

[<Emit("JSON.stringify($0, null, 2)")>]
let private stringify (value: obj) : string = nativeOnly

let private readSource (Path fileName as filePath) =
    Energy.Core.NodeInterop.readFileSyncSafe filePath (Encoding "utf8")
    |> Result.mapError (fun message -> SourceReadFailed(fileName, message))

/// Resolve, read, and analyze one workspace file for the export report.
let private analyzeFile rootDir grammar thresholds (Path fileName as file) =
    task {
        match Energy.Languages.Registry.resolveLanguageForFile fileName with
        | None -> return Error(UnsupportedLanguage fileName)
        | Some adapter ->
            match readSource file with
            | Error error -> return Error error
            | Ok source ->
                let! loaded = getOrLoadLanguage adapter.Id grammar

                return
                    loaded
                    |> Result.bind (function
                        | None -> Error(UnsupportedLanguage fileName)
                        | Some language -> analyzeSourceWith thresholds language fileName source)
                    |> Result.map (fun result ->
                        let fileResult: Energy.Core.Report.FileResult =
                            {
                                FilePath = relativePath rootDir file
                                Violations = result.Violations
                            }

                        fileResult)
    }

let rec private analyzeFiles rootDir grammar thresholds files =
    task {
        match files with
        | [] -> return Ok []
        | file :: remaining ->
            let! current = analyzeFile rootDir grammar thresholds file

            match current with
            | Error error -> return Error error
            | Ok result ->
                let! rest = analyzeFiles rootDir grammar thresholds remaining
                return rest |> Result.map (fun results -> result :: results)
    }

let private writeReport rootDir results =
    let reportDirectory = joinPath rootDir (Path ".energy-state")
    let reportPath = joinPath reportDirectory (Path "latest.sarif")

    try
        ensureDirectory reportDirectory
        writeTextFile reportPath (stringify (Energy.Core.ReportSarif.renderSarif results))
        Ok reportPath
    with error ->
        let (Path fileName) = reportPath
        Error(ReportWriteFailed(fileName, string<exn> error))

/// Resolve workspace files without letting a directory read reject the command task.
let private resolveFiles (Path root) =
    try
        Energy.Core.Scan.resolveSupportedFiles [ root ] root |> Ok
    with error ->
        Error(SourceReadFailed(root, string<exn> error))

/// Scan a workspace and write the report beneath its root, so relative SARIF locations resolve there.
let export (rootDir: Path) grammar thresholds : Task<Result<Path, AnalysisError>> =
    task {
        match resolveFiles rootDir with
        | Error error -> return Error error
        | Ok files ->
            let! analysis = analyzeFiles rootDir grammar thresholds files
            return analysis |> Result.bind (writeReport rootDir)
    }

let private workspaceRoot () =
    Energy.Extension.Vscode.Workspace.workspaceFolders workspace
    |> Option.ofObj
    |> Option.map (fun folders -> unbox<obj array> folders)
    |> Option.bind Array.tryHead
    |> Option.map (
        Energy.Extension.Vscode.Workspace.workspaceFolderUri
        >> Energy.Extension.Vscode.Document.uriFsPath
    )

/// Create the explicit workspace-export command without coupling it to extension lifecycle state.
let createCommand getGrammar () : Task<unit> =
    task {
        match workspaceRoot (), getGrammar () with
        | None, _ -> showErrorMessage window "Open a workspace folder before exporting a SARIF report."
        | _, None -> showErrorMessage window "Energy State Analyzer is not ready to export a SARIF report."
        | Some root, Some grammar ->
            let resource = Energy.Extension.Vscode.Presentation.uriFromFilePath root

            let! result =
                match Energy.Extension.Configuration.readAnalyzeThresholdsFor resource with
                | Error error -> Task.FromResult(Error error)
                | Ok thresholds -> export (Path root) grammar thresholds

            match result with
            | Error analysisError ->
                showErrorMessage window ("Could not export SARIF: " + analysisErrorMessage analysisError)
            | Ok reportPath ->
                let (Path reportFile) = reportPath

                let! _ =
                    showTextDocument window (Energy.Extension.Vscode.Presentation.uriFromFilePath reportFile)

                showInformationMessage window ("SARIF report exported to " + root + "/.energy-state/latest.sarif")
    }
