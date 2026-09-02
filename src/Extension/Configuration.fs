module Energy.Extension.Configuration

open Fable.Core.JS
open Energy.Core.Config
open Energy.Core.Paths
open Energy.Extension.ConfigurationValues
open Energy.Extension.Vscode.Document
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

// decision: load the project's .esaconfig.json once, cached from the workspace root, so every reader
// consults the same parsed instance; a missing folder/file yields None and readers fall straight to
// the vscode + default layers (preserving current editor behavior when no config exists).
let private cachedFileConfig = ref None

let private readCachedFileConfig () : FileConfig option =
    match !cachedFileConfig with
    | Some config -> Some config
    | None ->
        let resolved =
            workspaceFolders workspace
            |> Option.ofObj
            |> Option.filter (fun folders -> not (isNull folders))
            |> Option.map (fun folders -> unbox<obj array> folders)
            |> Option.filter (fun folders -> folders.Length > 0)
            |> Option.map (fun folders -> workspaceFolderUri folders.[0] |> uriFsPath |> Path)
            |> Option.bind findConfigFile
            |> Option.bind readConfigJson
            |> Option.map parseFileConfig

        cachedFileConfig := resolved
        resolved

// decision: a single combined SettingReader layers file-config over vscode settings over defaults, so
// one reader drives precedence (defaults < .esaconfig.json < host setting) for every threshold field.
let private fileInt (section: string) (key: string) (file: FileConfig) : int option =
    match section with
    | "nesting" ->
        if key = "mediumThreshold" then
            file.Nesting.MediumThreshold
        else
            file.Nesting.HighThreshold
    | "cyclomaticComplexity" ->
        if key = "mediumThreshold" then
            file.Cyclomatic.MediumThreshold
        else
            file.Cyclomatic.HighThreshold
    | "cognitiveComplexity" ->
        if key = "mediumThreshold" then
            file.Cognitive.MediumThreshold
        else
            file.Cognitive.HighThreshold
    | "coherence" ->
        match key with
        | "largeFunctionLines" -> file.Coherence.LargeFunctionLines
        | "maxLargeFunctions" -> file.Coherence.MaxLargeFunctions
        | _ -> None
    | "matchOpportunity" ->
        if key = "minBranches" then
            file.MatchOpportunity.MinBranches
        else
            None
    | "magicString" ->
        if key = "minDuplicates" then
            file.MagicString.MinDuplicates
        else
            None
    | _ -> None

let private fileFloat (section: string) (key: string) (file: FileConfig) : float option =
    match section with
    | "coherence" ->
        match key with
        | "singleDomainNameShare" -> file.Coherence.SingleDomainNameShare
        | "maxTypeDiversityRatio" -> file.Coherence.MaxTypeDiversityRatio
        | "minTypedCoverage" -> file.Coherence.MinTypedCoverage
        | _ -> None
    | _ -> None

let private reader =
    { Bool = fun section key fallback -> setting section key fallback
      Int =
        fun section key fallback ->
            readCachedFileConfig ()
            |> Option.bind (fileInt section key)
            |> Option.defaultValue (setting section key fallback)
      Float =
        fun section key fallback ->
            readCachedFileConfig ()
            |> Option.bind (fileFloat section key)
            |> Option.defaultValue (setting section key fallback)
      Floats =
        fun section key fallback ->
            match readCachedFileConfig () with
            | Some file when section = "magicNumber" && key = "allowlist" ->
                // decision: the project allowlist is authoritative for magic-number literals; the pure
                // mapping still unions structural literals on top, so both sources stay exempt.
                floatsFromConfiguration (Option.defaultValue [] file.MagicNumber.Allowlist |> List.toArray)
            | _ -> setting section key fallback
      String = fun section key fallback -> setting section key fallback
      Strings =
        fun section key fallback ->
            match readCachedFileConfig () with
            | Some file when section = "magicString" && key = "allowlist" ->
                Option.defaultValue [] file.MagicString.Allowlist
            | _ -> setting section key fallback
      GlobalBool = globalSetting }

let readAnalyzeThresholds () =
    ConfigurationValues.readAnalyzeThresholds reader

// decision: colors stay a VS Code setting — this reads them from the host only, never from .esaconfig.json.
let getEnergyColors () =
    ConfigurationValues.readEnergyColors reader

let includeFixtures () =
    getConfiguration workspace "energyStateAnalyzer"
    |> fun configuration -> getConfigurationValue configuration "includeFixtures" false
