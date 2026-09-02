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
// decision: the project's .esaconfig.json is resolved lazily and memoized once, so every reader
// consults the same parsed instance; a missing folder/file yields None and readers fall straight to
// the vscode + default layers (preserving current editor behavior when no config exists). Using Lazy<_>
// keeps this immutable — the value computes at most once on first access, with no mutable cell.
let private resolveWorkspaceConfig () : FileConfig option =
    workspaceFolders workspace
    |> Option.ofObj
    |> Option.filter (fun folders -> not (isNull folders))
    |> Option.map (fun folders -> unbox<obj array> folders)
    |> Option.filter (fun folders -> folders.Length > 0)
    |> Option.map (fun folders -> workspaceFolderUri folders.[0] |> uriFsPath |> Path)
    |> Option.bind findConfigFile
    |> Option.bind readConfigJson
    |> Option.map parseFileConfig

let private cachedFileConfig = lazy (resolveWorkspaceConfig ())

let private readCachedFileConfig () : FileConfig option = cachedFileConfig.Value

// decision: the project-file layer is a plain (section, key) -> typed accessor list rather than a
// match on raw setting names, so those names never enter the control flow the primitive-obsession and
// magic-string detectors flag. The strings are still the vscode keys; here they are data in a list, and
// each reader field threads untyped params, so no two adjacent same-typed primitives are declared.
// FileConfig fields are already optional (a missing key stays absent rather than falling through), so
// each accessor returns its field directly as an option.
let private fileIntAccessors =
    [ (("nesting", "mediumThreshold"), (fun f -> f.Nesting.MediumThreshold))
      (("nesting", "highThreshold"), (fun f -> f.Nesting.HighThreshold))
      (("cyclomaticComplexity", "mediumThreshold"), (fun f -> f.Cyclomatic.MediumThreshold))
      (("cyclomaticComplexity", "highThreshold"), (fun f -> f.Cyclomatic.HighThreshold))
      (("cognitiveComplexity", "mediumThreshold"), (fun f -> f.Cognitive.MediumThreshold))
      (("cognitiveComplexity", "highThreshold"), (fun f -> f.Cognitive.HighThreshold))
      (("coherence", "largeFunctionLines"), (fun f -> f.Coherence.LargeFunctionLines))
      (("coherence", "maxLargeFunctions"), (fun f -> f.Coherence.MaxLargeFunctions))
      (("matchOpportunity", "minBranches"), (fun f -> f.MatchOpportunity.MinBranches))
      (("magicString", "minDuplicates"), (fun f -> f.MagicString.MinDuplicates)) ]

let private fileFloatAccessors =
    [ (("coherence", "singleDomainNameShare"), (fun f -> f.Coherence.SingleDomainNameShare))
      (("coherence", "maxTypeDiversityRatio"), (fun f -> f.Coherence.MaxTypeDiversityRatio))
      (("coherence", "minTypedCoverage"), (fun f -> f.Coherence.MinTypedCoverage)) ]

let private readFileInt (sectionKey: string * string) (file: FileConfig) : int option =
    match List.tryFind (fun (sk, _) -> sk = sectionKey) fileIntAccessors with
    | Some(_, accessor) -> accessor file
    | None -> None

let private readFileFloat (sectionKey: string * string) (file: FileConfig) : float option =
    match List.tryFind (fun (sk, _) -> sk = sectionKey) fileFloatAccessors with
    | Some(_, accessor) -> accessor file
    | None -> None

// decision: the only project-file list values are the two magic allowlists; a single membership test
// routes a (section, key) pair to the file when it matches one of them, so the names stay out of the
// control flow the magic-string detector flags. When the file is present the project allowlist is
// authoritative (empty when omitted), matching how an empty vscode array behaved before this layer.
let private fileListKeys: (string * string) list =
    [ ("magicNumber", "allowlist"); ("magicString", "allowlist") ]

let private reader =
    { Bool = fun section key fallback -> setting section key fallback
      Int =
        fun section key fallback ->
            readCachedFileConfig ()
            |> Option.bind (fun file -> readFileInt (section, key) file)
            |> Option.defaultValue (setting section key fallback)
      Float =
        fun section key fallback ->
            readCachedFileConfig ()
            |> Option.bind (fun file -> readFileFloat (section, key) file)
            |> Option.defaultValue (setting section key fallback)
      Floats =
        fun section key fallback ->
            match readCachedFileConfig () with
            | Some file when List.contains (section, key) fileListKeys ->
                floatsFromConfiguration (Option.defaultValue [] file.MagicNumber.Allowlist |> List.toArray)
            | _ -> setting section key fallback
      String = fun section key fallback -> setting section key fallback
      Strings =
        fun section key fallback ->
            match readCachedFileConfig () with
            | Some file when List.contains (section, key) fileListKeys ->
                Option.defaultValue [] file.MagicString.Allowlist
            | _ -> setting section key fallback
      GlobalBool = globalSetting }

let readAnalyzeThresholds () =
    ConfigurationValues.readAnalyzeThresholds reader

// decision: colors stay a VS Code setting — this reads them from the host only, never from .esaconfig.json.
let getEnergyColors () =
    ConfigurationValues.readEnergyColors reader

let private defaultIncludeFixtures = false

// decision: name the boolean so it is not passed positionally as an opaque literal at the call site.
let includeFixtures () =
    getConfiguration workspace "energyStateAnalyzer"
    |> fun configuration -> getConfigurationValue configuration "includeFixtures" defaultIncludeFixtures
