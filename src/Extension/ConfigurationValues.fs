module Energy.Extension.ConfigurationValues

open Energy.Core.Analyze
open Energy.Core.Detectors.Cognitive
open Energy.Core.Detectors.Coherence
open Energy.Core.Detectors.Cyclomatic
open Energy.Core.Detectors.MagicNumber
open Energy.Core.Detectors.MagicString
open Energy.Core.Detectors.MatchOpportunity
open Energy.Core.Detectors.Nesting

type EnergyColors =
    { HighEnergy: string
      MediumEnergy: string
      LowEnergy: string
      BackgroundOpacity: float }

type SettingReader =
    { Bool: string -> string -> bool -> bool
      Int: string -> string -> int -> int
      Float: string -> string -> float -> float
      Floats: string -> string -> float list -> float list
      String: string -> string -> string -> string
      Strings: string -> string -> string list -> string list
      // decision: a top-level (non-namespaced) boolean reader for settings that apply across
      // detectors, e.g. includeTestFiles, which now governs both magic detectors.
      GlobalBool: string -> bool -> bool }

let defaultEnergyColors =
    { HighEnergy = "#fb8500"
      MediumEnergy = "#ffb703"
      LowEnergy = "#99dd99"
      BackgroundOpacity = 0.1 }

// decision: converts host configuration arrays at the extension boundary because Fable's list
// helpers require FSharpList values; passing VS Code's native arrays silently drops their values.
let floatsFromConfiguration (values: float array) : float list = values |> Seq.toList

let private magicNumberAllowlist (reader: SettingReader) =
    let defaults = Energy.Core.Detectors.MagicNumber.defaultOptions.Allowlist
    let configured = reader.Floats "magicNumber" "allowlist" defaults

    // decision: keeps the small structural literals exempt even when VS Code supplies an empty
    // configuration array; the setting extends the baseline policy with domain-specific values.
    defaults
    @ (configured |> List.filter (fun value -> not (List.contains value defaults)))

// decision: keeps the setting-name-to-core-options mapping pure and injectable so F# tests verify
// the public configuration contract without loading the VS Code host module.
let readAnalyzeThresholds (reader: SettingReader) : AnalyzeThresholds =
    { Nesting =
        { MediumThreshold = reader.Int "nesting" "mediumThreshold" defaultNestingThresholds.MediumThreshold
          HighThreshold = reader.Int "nesting" "highThreshold" defaultNestingThresholds.HighThreshold }
      Cyclomatic =
        { MediumThreshold =
            reader.Int "cyclomaticComplexity" "mediumThreshold" defaultCyclomaticThresholds.MediumThreshold
          HighThreshold = reader.Int "cyclomaticComplexity" "highThreshold" defaultCyclomaticThresholds.HighThreshold }
      Cognitive =
        { MediumThreshold =
            reader.Int "cognitiveComplexity" "mediumThreshold" defaultCognitiveThresholds.MediumThreshold
          HighThreshold = reader.Int "cognitiveComplexity" "highThreshold" defaultCognitiveThresholds.HighThreshold }
      Coherence =
        { LargeFunctionLines = reader.Int "coherence" "largeFunctionLines" defaultCoherenceThresholds.LargeFunctionLines
          MaxLargeFunctions = reader.Int "coherence" "maxLargeFunctions" defaultCoherenceThresholds.MaxLargeFunctions
          SingleDomainNameShare =
            reader.Float "coherence" "singleDomainNameShare" defaultCoherenceThresholds.SingleDomainNameShare
          MaxTypeDiversityRatio =
            reader.Float "coherence" "maxTypeDiversityRatio" defaultCoherenceThresholds.MaxTypeDiversityRatio
          MinTypedCoverage = reader.Float "coherence" "minTypedCoverage" defaultCoherenceThresholds.MinTypedCoverage }
      MatchOpportunity =
        { MinBranches =
            reader.Int
                "matchOpportunity"
                "minBranches"
                Energy.Core.Detectors.MatchOpportunity.defaultThresholds.MinBranches }
      MagicNumber =
        { Enabled = reader.Bool "magicNumber" "enabled" Energy.Core.Detectors.MagicNumber.defaultOptions.Enabled
          Allowlist = magicNumberAllowlist reader
          IncludeTestFiles =
            reader.GlobalBool "includeTestFiles" Energy.Core.Detectors.MagicNumber.defaultOptions.IncludeTestFiles }
      MagicString =
        { Enabled = reader.Bool "magicString" "enabled" Energy.Core.Detectors.MagicString.defaultOptions.Enabled
          MinDuplicates =
            reader.Int "magicString" "minDuplicates" Energy.Core.Detectors.MagicString.defaultOptions.MinDuplicates
          Allowlist =
            reader.Strings "magicString" "allowlist" Energy.Core.Detectors.MagicString.defaultOptions.Allowlist
          IncludeTestFiles =
            reader.GlobalBool "includeTestFiles" Energy.Core.Detectors.MagicString.defaultOptions.IncludeTestFiles } }

let readEnergyColors (reader: SettingReader) : EnergyColors =
    { HighEnergy = reader.String "colors" "highEnergy" defaultEnergyColors.HighEnergy
      MediumEnergy = reader.String "colors" "mediumEnergy" defaultEnergyColors.MediumEnergy
      LowEnergy = reader.String "colors" "lowEnergy" defaultEnergyColors.LowEnergy
      BackgroundOpacity = reader.Float "colors" "backgroundOpacity" defaultEnergyColors.BackgroundOpacity }
