module Energy.Extension.Configuration

open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Core.Paths
open Energy.Extension.ConfigurationValues
open Energy.Extension.Vscode.Document
open Energy.Extension.Vscode.Host
open Energy.Extension.Vscode.Workspace

/// Read a detector or color setting for a specific resource.
let private setting resource section key fallback =
    getConfigurationFor workspace ("energyStateAnalyzer." + section) resource
    |> fun configuration -> getConfigurationValue configuration key fallback

/// Read a top-level setting from the base energyStateAnalyzer configuration object.
///
/// decision: top-level settings live directly under energyStateAnalyzer (no sub-section), so the
/// global reader reads the base configuration object rather than a namespaced one.
let private globalSetting resource key fallback =
    getConfigurationFor workspace "energyStateAnalyzer" resource
    |> fun configuration -> getConfigurationValue configuration key fallback

let private workspaceRoot () =
    workspaceFolders workspace
    |> Option.ofObj
    |> Option.filter (fun folders -> not (isNull folders))
    |> Option.map (fun folders -> unbox<obj array> folders)
    |> Option.filter (fun folders -> folders.Length > 0)
    |> Option.map (fun folders -> workspaceFolderUri folders.[0] |> uriFsPath |> Path)

/// Resolve project configuration from the requested resource, avoiding cross-folder thresholds.
let private rootFor resource =
    if isNull resource then
        workspaceRoot ()
    else
        workspaceFolderFor workspace resource
        |> Option.ofObj
        |> Option.map (workspaceFolderUri >> uriFsPath >> Path)

/// Bind every setting lookup to the same resource as the project configuration.
let private reader resource =
    { Bool = setting resource
      Float = setting resource
      String = setting resource
      GlobalBool = globalSetting resource }

/// Read project thresholds and editor toggles for the document or export folder.
let readAnalyzeThresholdsFor resource : AnalyzeThresholds =
    rootFor resource
    |> Option.map loadAnalyzeOptions
    |> Option.defaultValue defaultAnalyzeOptions
    |> ConfigurationValues.readAnalyzeThresholds (reader resource)

/// Read thresholds before a document is available, using the first workspace folder.
let readAnalyzeThresholds () : AnalyzeThresholds = readAnalyzeThresholdsFor null

/// Read editor-only energy colors from the host settings.
///
/// decision: colors stay a VS Code setting — this reads them from the host only, never from .esaconfig.json.
let getEnergyColors () =
    ConfigurationValues.readEnergyColors (reader null)

let private defaultIncludeFixtures = false

/// Report whether magic-detector fixtures should be included, named for clarity at the call site.
///
/// decision: name the boolean so it is not passed positionally as an opaque literal at the call site.
let includeFixtures resource =
    getConfigurationFor workspace "energyStateAnalyzer" resource
    |> fun configuration -> getConfigurationValue configuration "includeFixtures" defaultIncludeFixtures
