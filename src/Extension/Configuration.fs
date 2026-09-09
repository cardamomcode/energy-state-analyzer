module Energy.Extension.Configuration

open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Core.Paths
open Energy.Extension.ConfigurationValues
open Energy.Extension.Vscode.Document
open Energy.Extension.Vscode.Host
open Energy.Extension.Vscode.Workspace

let private setting section key fallback =
    getConfiguration workspace ("energyStateAnalyzer." + section)
    |> fun configuration -> getConfigurationValue configuration key fallback

/// Read a top-level setting from the base energyStateAnalyzer configuration object.
///
/// decision: top-level settings live directly under energyStateAnalyzer (no sub-section), so the
/// global reader reads the base configuration object rather than a namespaced one.
let private globalSetting key fallback =
    getConfiguration workspace "energyStateAnalyzer"
    |> fun configuration -> getConfigurationValue configuration key fallback

let private workspaceRoot () =
    workspaceFolders workspace
    |> Option.ofObj
    |> Option.filter (fun folders -> not (isNull folders))
    |> Option.map (fun folders -> unbox<obj array> folders)
    |> Option.filter (fun folders -> folders.Length > 0)
    |> Option.map (fun folders -> workspaceFolderUri folders.[0] |> uriFsPath |> Path)

let private reader =
    { Bool = setting
      Float = setting
      String = setting
      GlobalBool = globalSetting }

let readAnalyzeThresholds () : AnalyzeThresholds =
    workspaceRoot ()
    |> Option.map loadAnalyzeOptions
    |> Option.defaultValue defaultAnalyzeOptions
    |> ConfigurationValues.readAnalyzeThresholds reader

/// Read editor-only energy colors from the host settings.
///
/// decision: colors stay a VS Code setting — this reads them from the host only, never from .esaconfig.json.
let getEnergyColors () =
    ConfigurationValues.readEnergyColors reader

let private defaultIncludeFixtures = false

/// Report whether magic-detector fixtures should be included, named for clarity at the call site.
///
/// decision: name the boolean so it is not passed positionally as an opaque literal at the call site.
let includeFixtures () =
    getConfiguration workspace "energyStateAnalyzer"
    |> fun configuration -> getConfigurationValue configuration "includeFixtures" defaultIncludeFixtures
