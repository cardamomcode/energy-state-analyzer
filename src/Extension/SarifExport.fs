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

let private readSource filePath = readFileSync filePath (Encoding "utf8")

let rec private analyzeFiles rootDir grammar thresholds files =
    task {
        match files with
        | [] -> return Ok []
        | file :: remaining ->
            let (Path fileName) = file

            match Energy.Languages.Registry.resolveLanguageForFile fileName with
            | None -> return Error(UnsupportedLanguage fileName)
            | Some adapter ->
                let source = readSource file
                let! loaded = getOrLoadLanguage adapter.Id grammar

                match loaded with
                | Error error -> return Error error
                | Ok None -> return Error(UnsupportedLanguage fileName)
                | Ok(Some language) ->
                    match analyzeSourceWith thresholds language fileName source with
                    | Error error -> return Error error
                    | Ok result ->
                        let! rest = analyzeFiles rootDir grammar thresholds remaining

                        return
                            rest
                            |> Result.map (fun results ->
                                let fileResult: Energy.Core.Report.FileResult =
                                    { FilePath = relativePath rootDir file
                                      Violations = result.Violations }

                                fileResult :: results)
    }

let private writeReport rootDir results =
    let reportDirectory = joinPath rootDir (Path ".energy-state")
    let reportPath = joinPath reportDirectory (Path "latest.sarif")
    ensureDirectory reportDirectory
    writeTextFile reportPath (stringify (Energy.Core.ReportSarif.renderSarif results))
    reportPath

/// Scan a workspace and write the report beneath its root, so relative SARIF locations resolve there.
let export (rootDir: Path) grammar thresholds : Task<Result<Path, AnalysisError>> =
    task {
        let (Path root) = rootDir
        let files = Energy.Core.Scan.resolveSupportedFiles [ root ] root
        let! analysis = analyzeFiles rootDir grammar thresholds files

        return analysis |> Result.map (writeReport rootDir)
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
            let thresholds = Energy.Extension.Configuration.readAnalyzeThresholdsFor resource
            let! result = export (Path root) grammar thresholds

            match result with
            | Error analysisError ->
                showErrorMessage window ("Could not export SARIF: " + analysisErrorMessage analysisError)
            | Ok reportPath ->
                let (Path reportFile) = reportPath
                let! _ = showTextDocument window (Energy.Extension.Vscode.Presentation.uriFromFilePath reportFile)

                showInformationMessage window ("SARIF report exported to " + root + "/.energy-state/latest.sarif")
    }
