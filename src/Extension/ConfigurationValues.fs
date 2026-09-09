module Energy.Extension.ConfigurationValues

open Energy.Core.Analyze
open Energy.Core.Config

type SettingReader =
    { Bool: string -> string -> bool -> bool
      Float: string -> string -> float -> float
      String: string -> string -> string -> string
      // decision: a top-level (non-namespaced) boolean reader for settings that apply across
      // detectors, e.g. includeTestFiles, which now governs both magic detectors.
      GlobalBool: string -> bool -> bool }

/// Apply only editor-specific toggles and test-file handling at the extension boundary.
///
/// decision: the project configuration is fully resolved by Core.Config before this boundary runs.
/// The extension changes only editor-specific toggles and test-file handling, so VS Code cannot make
/// its analysis disagree with the CLI or CI by overriding a project threshold or allowlist.
let readAnalyzeThresholds (reader: SettingReader) (options: AnalyzeThresholds) : AnalyzeThresholds =
    { options with
        Nesting =
            { options.Nesting with
                Enabled = reader.Bool "nesting" "enabled" options.Nesting.Enabled }
        Cyclomatic =
            { options.Cyclomatic with
                Enabled = reader.Bool "cyclomaticComplexity" "enabled" options.Cyclomatic.Enabled }
        Cognitive =
            { options.Cognitive with
                Enabled = reader.Bool "cognitiveComplexity" "enabled" options.Cognitive.Enabled }
        Coherence =
            { options.Coherence with
                Enabled = reader.Bool "coherence" "enabled" options.Coherence.Enabled }
        MatchOpportunity =
            { options.MatchOpportunity with
                Enabled = reader.Bool "matchOpportunity" "enabled" options.MatchOpportunity.Enabled }
        ParameterCount =
            { options.ParameterCount with
                Enabled = reader.Bool "parameterCount" "enabled" options.ParameterCount.Enabled }
        PrimitiveObsession =
            { options.PrimitiveObsession with
                Enabled = reader.Bool "primitiveObsession" "enabled" options.PrimitiveObsession.Enabled }
        OpaqueBoolean =
            { options.OpaqueBoolean with
                Enabled = reader.Bool "opaqueBoolean" "enabled" options.OpaqueBoolean.Enabled }
        LogicalControlFlow =
            { options.LogicalControlFlow with
                Enabled = reader.Bool "logicalControlFlow" "enabled" options.LogicalControlFlow.Enabled }
        Inversion =
            { options.Inversion with
                Enabled = reader.Bool "inversion" "enabled" options.Inversion.Enabled }
        ErrorShadowing =
            { options.ErrorShadowing with
                Enabled = reader.Bool "errorShadowing" "enabled" options.ErrorShadowing.Enabled }
        MagicNumber =
            { options.MagicNumber with
                Enabled = reader.Bool "magicNumber" "enabled" options.MagicNumber.Enabled
                IncludeTestFiles = reader.GlobalBool "includeTestFiles" options.MagicNumber.IncludeTestFiles }
        MagicString =
            { options.MagicString with
                Enabled = reader.Bool "magicString" "enabled" options.MagicString.Enabled
                IncludeTestFiles = reader.GlobalBool "includeTestFiles" options.MagicString.IncludeTestFiles } }

/// Read editor-only color settings, pulling defaults from Core.Config.
///
/// decision: colors stay a VS Code setting (not read from .esaconfig.json) — this mapping still pulls
/// its defaults from Core.Config, but the values themselves are host-only.
let readEnergyColors (reader: SettingReader) : EnergyColors =
    { HighEnergy = reader.String "colors" "highEnergy" defaultEnergyColors.HighEnergy
      MediumEnergy = reader.String "colors" "mediumEnergy" defaultEnergyColors.MediumEnergy
      LowEnergy = reader.String "colors" "lowEnergy" defaultEnergyColors.LowEnergy
      BackgroundOpacity = reader.Float "colors" "backgroundOpacity" defaultEnergyColors.BackgroundOpacity }
