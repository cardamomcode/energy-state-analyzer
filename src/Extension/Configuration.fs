module Energy.Extension.Configuration

open Energy.Extension.ConfigurationValues
open Energy.Extension.Vscode.Host
open Energy.Extension.Vscode.Workspace

let private setting section key fallback =
    getConfiguration workspace ("energyStateAnalyzer." + section)
    |> fun configuration -> getConfigurationValue configuration key fallback

// decision: top-level settings live directly under energyStateAnalyzer (no sub-section), so the
// global reader reads the base configuration object rather than a namespaced one.
let private globalSetting key fallback =
    getConfiguration workspace "energyStateAnalyzer"
    |> fun configuration -> getConfigurationValue configuration key fallback

let private reader =
    { Bool = setting
      Int = setting
      Float = setting
      Floats = setting
      String = setting
      Strings = setting
      GlobalBool = globalSetting }

let readAnalyzeThresholds () =
    ConfigurationValues.readAnalyzeThresholds reader

let getEnergyColors () =
    ConfigurationValues.readEnergyColors reader

let includeFixtures () =
    getConfiguration workspace "energyStateAnalyzer"
    |> fun configuration -> getConfigurationValue configuration "includeFixtures" false
