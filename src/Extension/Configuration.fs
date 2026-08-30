module Energy.Extension.Configuration

open Energy.Extension.ConfigurationValues
open Energy.Extension.VscodeHost
open Energy.Extension.VscodeWorkspace

let private setting section key fallback =
    getConfiguration workspace ("energyStateAnalyzer." + section)
    |> fun configuration -> getConfigurationValue configuration key fallback

let private reader =
    { Bool = setting
      Int = setting
      Float = setting
      Floats = setting
      String = setting
      Strings = setting }

let readAnalyzeThresholds () = ConfigurationValues.readAnalyzeThresholds reader

let getEnergyColors () = ConfigurationValues.readEnergyColors reader

let includeFixtures () =
    getConfiguration workspace "energyStateAnalyzer"
    |> fun configuration -> getConfigurationValue configuration "includeFixtures" false
