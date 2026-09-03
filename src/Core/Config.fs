module Energy.Core.Config

// decision: this file is the single source of truth for every analyzer config value and the loader
// that overlays a project's `.esaconfig.json` on top of those defaults. Keeping all thresholds,
// allowlists, ratios, and color hexes here — rather than as module vars scattered across Context,
// the detectors, and the extension — means exactly one place defines what "default" means, so a
// threshold can no longer drift between the editor, the CLI, and this file's baked-in values.

open Fable.Core
open Energy.Core.Paths
open Energy.Core.FsPath

// ---------------------------------------------------------------------------
// a) Consolidated defaults: every threshold type + value, allowlist, ratio, and color.
//
// These are all primitive records (int/float/bool/string/lists), so this block is data — `let`
// bindings, not functions — and leaves the rest of Core free to import from here instead of defining
// its own config values. The named aliases below let hosts keep importing one small value at a time.
// ---------------------------------------------------------------------------

type NestingThresholds =
    { MediumThreshold: int
      HighThreshold: int }

type CyclomaticThresholds =
    { MediumThreshold: int
      HighThreshold: int }

type CognitiveThresholds =
    { MediumThreshold: int
      HighThreshold: int }

type CoherenceThresholds =
    { LargeFunctionLines: int
      MaxLargeFunctions: int
      SingleDomainNameShare: float
      MaxTypeDiversityRatio: float
      MinTypedCoverage: float
      SiblingOpenThreshold: int
      ImportBreadthThreshold: int
      HighImportBreadthThreshold: int
      MemberImportFanOutThreshold: int
      UtilsFileFunctionCount: int
      GenericFunctionCount: int
      HighFunctionCount: int
      MethodCountMedium: int
      MethodCountHigh: int
      LargeFunctionSeverityMultiplier: float }

type MatchOpportunityThresholds = { MinBranches: int }

type ParameterCountThresholds =
    { MediumThreshold: int
      HighThreshold: int }

type MagicNumberOptions =
    { Enabled: bool
      Allowlist: float list
      IncludeTestFiles: bool }

type MagicStringOptions =
    { Enabled: bool
      MinDuplicates: int
      Allowlist: string list
      IncludeTestFiles: bool }

/// Host-independent settings supplied once per analysis request.
type AnalyzeOptions =
    { Nesting: NestingThresholds
      Cyclomatic: CyclomaticThresholds
      Cognitive: CognitiveThresholds
      Coherence: CoherenceThresholds
      MatchOpportunity: MatchOpportunityThresholds
      ParameterCount: ParameterCountThresholds
      MagicNumber: MagicNumberOptions
      MagicString: MagicStringOptions }

// decision: default nesting thresholds 3/5 mark the point where active conditions strain working memory.
// decision: default cyclomatic thresholds 10/15 distinguish many paths from urgent extraction work.
// decision: default cognitive thresholds 15/25 align the nesting-weighted metric with SonarSource defaults.
// decision: coherence uses large-function count rather than raw function count because F# modules often have many small functions.
// decision: the function-count sprawl thresholds (8 for utils/helper/common-named files, 12 generic, 15 high-severity) and the large-function severity multiplier are configurable so a project can retune exactly where sprawl is flagged without editing detector code.
// decision: the god-class method-count bars (15 medium / 25 high) are configurable for the same reason — a project can retune where a single type's responsibility sprawl is flagged without editing the class-relatedness detector.
// decision: magic-detector test files stay exempt by default because their literals are usually intentional.
// decision: parameter-count thresholds 5/8 mark the point where callers can no longer recall argument order without the signature.
let defaultAnalyzeOptions =
    { Nesting =
        { MediumThreshold = 3
          HighThreshold = 5 }
      Cyclomatic =
        { MediumThreshold = 10
          HighThreshold = 15 }
      Cognitive =
        { MediumThreshold = 15
          HighThreshold = 25 }
      Coherence =
        { LargeFunctionLines = 20
          MaxLargeFunctions = 5
          SingleDomainNameShare = 0.7
          MaxTypeDiversityRatio = 0.4
          MinTypedCoverage = 0.5
          SiblingOpenThreshold = 7
          ImportBreadthThreshold = 10
          HighImportBreadthThreshold = 15
          MemberImportFanOutThreshold = 10
          UtilsFileFunctionCount = 8
          GenericFunctionCount = 12
          HighFunctionCount = 15
          MethodCountMedium = 15
          MethodCountHigh = 25
          LargeFunctionSeverityMultiplier = 1.5 }
      MatchOpportunity = { MinBranches = 3 }
      ParameterCount =
        { MediumThreshold = 5
          HighThreshold = 8 }
      MagicNumber =
        { Enabled = true
          Allowlist = [ 0.0; 1.0; -1.0; 2.0 ]
          IncludeTestFiles = false }
      MagicString =
        { Enabled = true
          MinDuplicates = 2
          Allowlist = [ ""; "utf-8"; "__main__" ]
          IncludeTestFiles = false } }

let defaultNestingThresholds = defaultAnalyzeOptions.Nesting

let defaultCyclomaticThresholds = defaultAnalyzeOptions.Cyclomatic

let defaultCognitiveThresholds = defaultAnalyzeOptions.Cognitive

let defaultCoherenceThresholds = defaultAnalyzeOptions.Coherence

let defaultMatchOpportunityThresholds = defaultAnalyzeOptions.MatchOpportunity

let defaultParameterCountThresholds = defaultAnalyzeOptions.ParameterCount

let defaultMagicNumberOptions = defaultAnalyzeOptions.MagicNumber

let defaultMagicStringOptions = defaultAnalyzeOptions.MagicString

// decision: color defaults live here (not in the extension) so there is one definition of "default
// amber"; the separate "colors stay a VS Code setting" decision only means colors are never *read*
// from .esaconfig.json at runtime — this is where they are declared, not where hosts pull them from.
type EnergyColors =
    { HighEnergy: string
      MediumEnergy: string
      LowEnergy: string
      BackgroundOpacity: float }

let defaultEnergyColors =
    { HighEnergy = "#fb8500"
      MediumEnergy = "#ffb703"
      LowEnergy = "#99dd99"
      BackgroundOpacity = 0.1 }

// ---------------------------------------------------------------------------
// b) Load .esaconfig.json over those defaults. Fable-safe fs + path bindings via the FsPath/Paths
// facade (the Esaignore.fs idiom), so one file drives both the editor and CI.
// ---------------------------------------------------------------------------

let configFileName = ".esaconfig.json"

// A parsed project config: an optional field per section, so a key can no longer be transposed with
// its value and an absent key simply falls back to the default during merge. Public so the extension's
// combined reader (Configuration.fs) can layer these values under vscode settings before merging.
type FileNesting =
    { MediumThreshold: int option
      HighThreshold: int option }

type FileCyclomatic =
    { MediumThreshold: int option
      HighThreshold: int option }

type FileCognitive =
    { MediumThreshold: int option
      HighThreshold: int option }

type FileCoherence =
    { LargeFunctionLines: int option
      MaxLargeFunctions: int option
      SingleDomainNameShare: float option
      MaxTypeDiversityRatio: float option
      MinTypedCoverage: float option
      SiblingOpenThreshold: int option
      ImportBreadthThreshold: int option
      HighImportBreadthThreshold: int option
      MemberImportFanOutThreshold: int option
      UtilsFileFunctionCount: int option
      GenericFunctionCount: int option
      HighFunctionCount: int option
      MethodCountMedium: int option
      MethodCountHigh: int option
      LargeFunctionSeverityMultiplier: float option }

type FileMatchOpportunity = { MinBranches: int option }

type FileParameterCount =
    { MediumThreshold: int option
      HighThreshold: int option }

type FileMagicNumber = { Allowlist: float list option }

type FileMagicString =
    { MinDuplicates: int option
      Allowlist: string list option }

type FileConfig =
    { Nesting: FileNesting
      Cyclomatic: FileCyclomatic
      Cognitive: FileCognitive
      Coherence: FileCoherence
      MatchOpportunity: FileMatchOpportunity
      ParameterCount: FileParameterCount
      MagicNumber: FileMagicNumber
      MagicString: FileMagicString }

let private emptyFileConfig: FileConfig =
    { Nesting =
        { MediumThreshold = None
          HighThreshold = None }
      Cyclomatic =
        { MediumThreshold = None
          HighThreshold = None }
      Cognitive =
        { MediumThreshold = None
          HighThreshold = None }
      Coherence =
        { LargeFunctionLines = None
          MaxLargeFunctions = None
          SingleDomainNameShare = None
          MaxTypeDiversityRatio = None
          MinTypedCoverage = None
          SiblingOpenThreshold = None
          ImportBreadthThreshold = None
          HighImportBreadthThreshold = None
          MemberImportFanOutThreshold = None
          UtilsFileFunctionCount = None
          GenericFunctionCount = None
          HighFunctionCount = None
          MethodCountMedium = None
          MethodCountHigh = None
          LargeFunctionSeverityMultiplier = None }
      MatchOpportunity = { MinBranches = None }
      ParameterCount =
        { MediumThreshold = None
          HighThreshold = None }
      MagicNumber = { Allowlist = None }
      MagicString =
        { MinDuplicates = None
          Allowlist = None } }

// decision: property access stays as two tiny `[<Emit>]` bindings rather than casting through a Map,
// so arbitrary nested JSON navigates without Fable turning plain objects into .NET Maps.
[<Emit("$0[$1]")>]
let private getProp (value: obj) (key: string) : obj = nativeOnly

[<Emit("$0 == null")>]
let private isNullOrUndefined (value: obj) : bool = nativeOnly

[<Emit("JSON.parse($0)")>]
let private jsonParse (text: string) : obj = nativeOnly

// Read one property of a JSON object as an opaque value, None when the key is absent or null.
let private field (parent: obj) (key: string) : obj option =
    let value = getProp parent key

    if isNullOrUndefined value then None else Some value

// decision: JSON has no int/float distinction at runtime — every number is a JS number — so read all
// numerics as float (the safe unbox) and narrow to int only where a field is declared an int.
let private readNumber (section: obj option) (key: string) : float option =
    section
    |> Option.bind (fun section -> field section key)
    |> Option.map unbox<float>

let private readList (section: obj option) (key: string) : obj list option =
    section
    |> Option.bind (fun section -> field section key)
    |> Option.map (fun value -> unbox<obj array> value |> List.ofArray)

// decision: walk up parent directories from the start dir until .esaconfig.json is found or root is
// reached — the same ".gitignore" discovery a linter expects, so one file configures every subtree.
let findConfigFile (startDir: Path) : Path option =
    let rec walkUp (dir: Path) : Path option =
        let candidate = joinPath dir (Path configFileName)

        if existsSync candidate then
            Some candidate
        else
            let parent = dirname dir

            // dirname returns its input at the filesystem root (e.g. "/" or "C:\"), so equal means done.
            if isNullOrUndefined parent || parent = dir then
                None
            else
                walkUp parent

    walkUp startDir

// decision: a missing file yields None (not an error) and malformed JSON is swallowed, so a project
// with no .esaconfig.json — or one with a typo — simply keeps the built-in defaults.
let readConfigJson (path: Path) : obj option =
    if not (existsSync path) then
        None
    else
        try
            let parsed = jsonParse (readFileSync path (Encoding "utf8"))

            if isNullOrUndefined parsed then None else Some parsed
        with _ ->
            None

let parseFileConfig (raw: obj) : FileConfig =
    let nesting = field raw "nesting"
    let cyclomatic = field raw "cyclomaticComplexity"
    let cognitive = field raw "cognitiveComplexity"
    let coherence = field raw "coherence"
    let matchOpportunity = field raw "matchOpportunity"
    let parameterCount = field raw "parameterCount"
    let magicNumber = field raw "magicNumber"
    let magicString = field raw "magicString"

    { Nesting =
        { MediumThreshold = readNumber nesting "mediumThreshold" |> Option.map int
          HighThreshold = readNumber nesting "highThreshold" |> Option.map int }
      Cyclomatic =
        { MediumThreshold = readNumber cyclomatic "mediumThreshold" |> Option.map int
          HighThreshold = readNumber cyclomatic "highThreshold" |> Option.map int }
      Cognitive =
        { MediumThreshold = readNumber cognitive "mediumThreshold" |> Option.map int
          HighThreshold = readNumber cognitive "highThreshold" |> Option.map int }
      Coherence =
        { LargeFunctionLines = readNumber coherence "largeFunctionLines" |> Option.map int
          MaxLargeFunctions = readNumber coherence "maxLargeFunctions" |> Option.map int
          SingleDomainNameShare = readNumber coherence "singleDomainNameShare"
          MaxTypeDiversityRatio = readNumber coherence "maxTypeDiversityRatio"
          MinTypedCoverage = readNumber coherence "minTypedCoverage"
          SiblingOpenThreshold = readNumber coherence "siblingOpenThreshold" |> Option.map int
          ImportBreadthThreshold = readNumber coherence "importBreadthThreshold" |> Option.map int
          HighImportBreadthThreshold = readNumber coherence "highImportBreadthThreshold" |> Option.map int
          MemberImportFanOutThreshold = readNumber coherence "memberImportFanOutThreshold" |> Option.map int
          UtilsFileFunctionCount = readNumber coherence "utilsFileFunctionCount" |> Option.map int
          GenericFunctionCount = readNumber coherence "genericFunctionCount" |> Option.map int
          HighFunctionCount = readNumber coherence "highFunctionCount" |> Option.map int
          MethodCountMedium = readNumber coherence "godClassMethodCountMedium" |> Option.map int
          MethodCountHigh = readNumber coherence "godClassMethodCountHigh" |> Option.map int
          LargeFunctionSeverityMultiplier = readNumber coherence "largeFunctionSeverityMultiplier" }
      MatchOpportunity = { MinBranches = readNumber matchOpportunity "minBranches" |> Option.map int }
      ParameterCount =
        { MediumThreshold = readNumber parameterCount "mediumThreshold" |> Option.map int
          HighThreshold = readNumber parameterCount "highThreshold" |> Option.map int }
      MagicNumber = { Allowlist = readList magicNumber "allowlist" |> Option.map (List.map unbox<float>) }
      MagicString =
        { MinDuplicates = readNumber magicString "minDuplicates" |> Option.map int
          Allowlist = readList magicString "allowlist" |> Option.map (List.map unbox<string>) } }

// decision: a provided allowlist is unioned with the structural/sentinel literals rather than
// replacing them, so 0/1/-1/2 (magic number) and "" / "utf-8" / "__main__" (magic string) stay
// exempt no matter what a project sets — matching how the extension already extends its baseline.
let mergeOptions (defaults: AnalyzeOptions) (file: FileConfig) : AnalyzeOptions =
    let structuralMagicNumberAllowlist = defaults.MagicNumber.Allowlist

    let unionWithStructuralMagicNumber provided =
        structuralMagicNumberAllowlist
        @ (provided
           |> List.filter (fun value -> not (List.contains value structuralMagicNumberAllowlist)))

    let structuralMagicStringAllowlist = defaults.MagicString.Allowlist

    let unionWithStructuralMagicString provided =
        structuralMagicStringAllowlist
        @ (provided
           |> List.filter (fun value -> not (List.contains value structuralMagicStringAllowlist)))

    { Nesting =
        { MediumThreshold = Option.defaultValue defaults.Nesting.MediumThreshold file.Nesting.MediumThreshold
          HighThreshold = Option.defaultValue defaults.Nesting.HighThreshold file.Nesting.HighThreshold }
      Cyclomatic =
        { MediumThreshold = Option.defaultValue defaults.Cyclomatic.MediumThreshold file.Cyclomatic.MediumThreshold
          HighThreshold = Option.defaultValue defaults.Cyclomatic.HighThreshold file.Cyclomatic.HighThreshold }
      Cognitive =
        { MediumThreshold = Option.defaultValue defaults.Cognitive.MediumThreshold file.Cognitive.MediumThreshold
          HighThreshold = Option.defaultValue defaults.Cognitive.HighThreshold file.Cognitive.HighThreshold }
      Coherence =
        { LargeFunctionLines =
            Option.defaultValue defaults.Coherence.LargeFunctionLines file.Coherence.LargeFunctionLines
          MaxLargeFunctions = Option.defaultValue defaults.Coherence.MaxLargeFunctions file.Coherence.MaxLargeFunctions
          SingleDomainNameShare =
            Option.defaultValue defaults.Coherence.SingleDomainNameShare file.Coherence.SingleDomainNameShare
          MaxTypeDiversityRatio =
            Option.defaultValue defaults.Coherence.MaxTypeDiversityRatio file.Coherence.MaxTypeDiversityRatio
          MinTypedCoverage = Option.defaultValue defaults.Coherence.MinTypedCoverage file.Coherence.MinTypedCoverage
          SiblingOpenThreshold =
            Option.defaultValue defaults.Coherence.SiblingOpenThreshold file.Coherence.SiblingOpenThreshold
          ImportBreadthThreshold =
            Option.defaultValue defaults.Coherence.ImportBreadthThreshold file.Coherence.ImportBreadthThreshold
          HighImportBreadthThreshold =
            Option.defaultValue defaults.Coherence.HighImportBreadthThreshold file.Coherence.HighImportBreadthThreshold
          MemberImportFanOutThreshold =
            Option.defaultValue
                defaults.Coherence.MemberImportFanOutThreshold
                file.Coherence.MemberImportFanOutThreshold
          UtilsFileFunctionCount =
            Option.defaultValue defaults.Coherence.UtilsFileFunctionCount file.Coherence.UtilsFileFunctionCount
          GenericFunctionCount =
            Option.defaultValue defaults.Coherence.GenericFunctionCount file.Coherence.GenericFunctionCount
          HighFunctionCount = Option.defaultValue defaults.Coherence.HighFunctionCount file.Coherence.HighFunctionCount
          MethodCountMedium = Option.defaultValue defaults.Coherence.MethodCountMedium file.Coherence.MethodCountMedium
          MethodCountHigh = Option.defaultValue defaults.Coherence.MethodCountHigh file.Coherence.MethodCountHigh
          LargeFunctionSeverityMultiplier =
            Option.defaultValue
                defaults.Coherence.LargeFunctionSeverityMultiplier
                file.Coherence.LargeFunctionSeverityMultiplier }
      MatchOpportunity =
        { MinBranches = Option.defaultValue defaults.MatchOpportunity.MinBranches file.MatchOpportunity.MinBranches }
      ParameterCount =
        { MediumThreshold =
            Option.defaultValue defaults.ParameterCount.MediumThreshold file.ParameterCount.MediumThreshold
          HighThreshold = Option.defaultValue defaults.ParameterCount.HighThreshold file.ParameterCount.HighThreshold }
      MagicNumber =
        { defaults.MagicNumber with
            Allowlist =
                (Option.defaultValue [] file.MagicNumber.Allowlist)
                |> unionWithStructuralMagicNumber }
      MagicString =
        { defaults.MagicString with
            MinDuplicates = Option.defaultValue defaults.MagicString.MinDuplicates file.MagicString.MinDuplicates
            Allowlist =
                (Option.defaultValue [] file.MagicString.Allowlist)
                |> unionWithStructuralMagicString } }

/// Public entry: resolve `.esaconfig.json` from `startDir` (walking up) and overlay it on the defaults.
///
// decision: precedence is `defaults < .esaconfig.json < host override`, so a project file configures
// both editor and CI while each host still wins at its own boundary (vscode settings, CLI flags).
let loadAnalyzeOptions (startDir: Path) : AnalyzeOptions =
    let fileConfig =
        findConfigFile startDir
        |> Option.bind readConfigJson
        |> Option.map parseFileConfig
        |> Option.defaultValue emptyFileConfig

    mergeOptions defaultAnalyzeOptions fileConfig

// decision: load from an explicit config-file path (the CLI's --config flag) instead of searching
// upward; returns the defaults when the file is missing or unreadable so callers can fall back.
let loadAnalyzeOptionsFromConfigPath (path: Path) : AnalyzeOptions =
    match readConfigJson path with
    | Some config -> mergeOptions defaultAnalyzeOptions (parseFileConfig config)
    | None -> defaultAnalyzeOptions
