module Energy.Extension.ConfigurationValues

open Energy.Core.Analyze
open Energy.Core.Config

// decision: the threshold/color defaults now live in Core.Config as the single source of truth; this
// extension module imports them (rather than defining its own copy) so the editor and CLI can never
// drift from one another. The pure mapping below stays injectable — it takes a SettingReader, not the
// host — so F# tests verify the configuration contract without loading the VS Code module.

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

// decision: converts host configuration arrays at the extension boundary because Fable's list
// helpers require FSharpList values; passing VS Code's native arrays silently drops their values.
let floatsFromConfiguration (values: float array) : float list = values |> Seq.toList

let private magicNumberAllowlist (reader: SettingReader) =
    let defaults = defaultMagicNumberOptions.Allowlist
    let configured = reader.Floats "magicNumber" "allowlist" defaults

    // decision: keeps the small structural literals exempt even when VS Code supplies an empty
    // configuration array; the setting extends the baseline policy with domain-specific values.
    defaults
    @ (configured |> List.filter (fun value -> not (List.contains value defaults)))

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
          MinTypedCoverage = reader.Float "coherence" "minTypedCoverage" defaultCoherenceThresholds.MinTypedCoverage
          SiblingOpenThreshold =
            reader.Int "coherence" "siblingOpenThreshold" defaultCoherenceThresholds.SiblingOpenThreshold
          ImportBreadthThreshold =
            reader.Int "coherence" "importBreadthThreshold" defaultCoherenceThresholds.ImportBreadthThreshold
          HighImportBreadthThreshold =
            reader.Int "coherence" "highImportBreadthThreshold" defaultCoherenceThresholds.HighImportBreadthThreshold
          MemberImportFanOutThreshold =
            reader.Int "coherence" "memberImportFanOutThreshold" defaultCoherenceThresholds.MemberImportFanOutThreshold
          UtilsFileFunctionCount =
            reader.Int "coherence" "utilsFileFunctionCount" defaultCoherenceThresholds.UtilsFileFunctionCount
          GenericFunctionCount =
            reader.Int "coherence" "genericFunctionCount" defaultCoherenceThresholds.GenericFunctionCount
          HighFunctionCount = reader.Int "coherence" "highFunctionCount" defaultCoherenceThresholds.HighFunctionCount
          MethodCountMedium =
            reader.Int "coherence" "godClassMethodCountMedium" defaultCoherenceThresholds.MethodCountMedium
          MethodCountHigh = reader.Int "coherence" "godClassMethodCountHigh" defaultCoherenceThresholds.MethodCountHigh
          LargeFunctionSeverityMultiplier =
            reader.Float
                "coherence"
                "largeFunctionSeverityMultiplier"
                defaultCoherenceThresholds.LargeFunctionSeverityMultiplier }
      MatchOpportunity =
        { MinBranches = reader.Int "matchOpportunity" "minBranches" defaultMatchOpportunityThresholds.MinBranches }
      ParameterCount =
        { MediumThreshold =
            reader.Int "parameterCount" "mediumThreshold" defaultParameterCountThresholds.MediumThreshold
          HighThreshold = reader.Int "parameterCount" "highThreshold" defaultParameterCountThresholds.HighThreshold }
      MagicNumber =
        { Enabled = reader.Bool "magicNumber" "enabled" defaultMagicNumberOptions.Enabled
          Allowlist = magicNumberAllowlist reader
          IncludeTestFiles = reader.GlobalBool "includeTestFiles" defaultMagicNumberOptions.IncludeTestFiles }
      MagicString =
        { Enabled = reader.Bool "magicString" "enabled" defaultMagicStringOptions.Enabled
          MinDuplicates = reader.Int "magicString" "minDuplicates" defaultMagicStringOptions.MinDuplicates
          Allowlist = reader.Strings "magicString" "allowlist" defaultMagicStringOptions.Allowlist
          IncludeTestFiles = reader.GlobalBool "includeTestFiles" defaultMagicStringOptions.IncludeTestFiles } }

// decision: colors stay a VS Code setting (not read from .esaconfig.json) — this mapping still pulls
// its defaults from Core.Config, but the values themselves are host-only.
let readEnergyColors (reader: SettingReader) : EnergyColors =
    { HighEnergy = reader.String "colors" "highEnergy" defaultEnergyColors.HighEnergy
      MediumEnergy = reader.String "colors" "mediumEnergy" defaultEnergyColors.MediumEnergy
      LowEnergy = reader.String "colors" "lowEnergy" defaultEnergyColors.LowEnergy
      BackgroundOpacity = reader.Float "colors" "backgroundOpacity" defaultEnergyColors.BackgroundOpacity }
