module Energy.Cli

open System
open System.Threading.Tasks

open Energy.CliModes
open Energy.CliNode
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Core.Paths

type private ParsedArguments =
    { Paths: string list
      BaseRef: string option
      Report: ReportFormat option
      ConfigFile: string option
      Nesting: int option * int option
      Cyclomatic: int option * int option
      Cognitive: int option * int option
      IncludeTestFiles: bool }

let private valueFlags =
    Set.ofList
        [ "base-ref"
          "report"
          "config"
          "medium-nesting"
          "high-nesting"
          "medium-cyclomatic"
          "high-cyclomatic"
          "medium-cognitive"
          "high-cognitive" ]

// decision: recognized flags consume exactly one following value, leaving all other positional
// arguments untouched so the CLI retains its intentionally small dependency-free parser.
let private parseValues (arguments: string array) : string list * Map<string, string> =
    // decision: boolean flags are recognized by name and consume no value, so they can appear
    // anywhere in the argument list without shifting the positional paths.
    let booleanFlags = Set.ofList [ "include-test-files" ]

    let rec loop (index: int) (paths: string list) (flags: Map<string, string>) =
        if index >= arguments.Length then
            List.rev paths, flags
        else
            let argument = arguments.[index]

            if argument.StartsWith "--" && booleanFlags.Contains(argument.Substring 2) then
                loop (index + 1) paths (Map.add (argument.Substring 2) "true" flags)
            elif
                argument.StartsWith "--"
                && valueFlags.Contains(argument.Substring 2)
                && index + 1 < arguments.Length
            then
                loop (index + 2) paths (Map.add (argument.Substring 2) arguments.[index + 1] flags)
            elif argument.StartsWith "--" then
                loop (index + 1) paths flags
            else
                loop (index + 1) (argument :: paths) flags

    loop 0 [] Map.empty

let private asNumber (flags: Map<string, string>) name =
    flags
    |> Map.tryFind name
    |> Option.bind (fun value ->
        match Int32.TryParse value with
        | true, number -> Some number
        | false, _ -> None)

let private parseArguments arguments =
    let paths, flags = parseValues arguments

    { Paths = paths
      BaseRef = Map.tryFind "base-ref" flags
      Report = Map.tryFind "report" flags
      ConfigFile = Map.tryFind "config" flags
      Nesting = asNumber flags "medium-nesting", asNumber flags "high-nesting"
      Cyclomatic = asNumber flags "medium-cyclomatic", asNumber flags "high-cyclomatic"
      Cognitive = asNumber flags "medium-cognitive", asNumber flags "high-cognitive"
      IncludeTestFiles = Map.containsKey "include-test-files" flags }

let private thresholdOverride (defaultMedium, defaultHigh) constructor (medium, high) =
    constructor (Option.defaultValue defaultMedium medium) (Option.defaultValue defaultHigh high)

// decision: the CLI reads .esaconfig.json by default (searching up from the current directory), or an
// explicit path via --config; threshold flags then override whatever that file set. This is why the
// defaults now come from Core.Config — they are the same values a project's config overlays onto.
let private buildThresholds parsed : AnalyzeOptions =
    let baseOptions =
        match parsed.ConfigFile with
        | Some configPath -> loadAnalyzeOptionsFromConfigPath (Path configPath)
        | None -> loadAnalyzeOptions (Path(cwd ()))

    { baseOptions with
        // decision: the allowlists, enabled flags, and min-duplicates all come from baseOptions, which
        // already merged .esaconfig.json over the Core.Config defaults; only the --include-test-files flag
        // overrides them, so a project's config can no longer be silently discarded by the CLI.
        MagicNumber =
            { baseOptions.MagicNumber with
                IncludeTestFiles = parsed.IncludeTestFiles }
        MagicString =
            { baseOptions.MagicString with
                IncludeTestFiles = parsed.IncludeTestFiles }
        Nesting =
            thresholdOverride
                (defaultNestingThresholds.MediumThreshold, defaultNestingThresholds.HighThreshold)
                (fun medium high ->
                    { MediumThreshold = medium
                      HighThreshold = high })
                parsed.Nesting
        Cyclomatic =
            thresholdOverride
                (defaultCyclomaticThresholds.MediumThreshold, defaultCyclomaticThresholds.HighThreshold)
                (fun medium high ->
                    { MediumThreshold = medium
                      HighThreshold = high })
                parsed.Cyclomatic
        Cognitive =
            thresholdOverride
                (defaultCognitiveThresholds.MediumThreshold, defaultCognitiveThresholds.HighThreshold)
                (fun medium high ->
                    { MediumThreshold = medium
                      HighThreshold = high })
                parsed.Cognitive }

let runCli () : Task<unit> =
    task {
        try
            let parsed = parseArguments (argv ())
            let thresholds = buildThresholds parsed

            let report =
                parsed.Report
                |> Option.defaultValue (
                    if parsed.BaseRef.IsSome || parsed.Paths.Length <> 1 then
                        "md"
                    else
                        "json"
                )

            match parsed.BaseRef, parsed.Paths with
            | Some baseRef, _ -> do! runDiff baseRef parsed.Paths thresholds report
            | None, [] ->
                printUsage ()
                exit 2
            | None, [ path ] when existsSync (Path path) && isFile (statSync (Path path)) && parsed.Report.IsNone ->
                do! runLegacySingleFile path thresholds
            | None, _ -> do! runScan parsed.Paths thresholds report
        with error ->
            Energy.CliNode.error ("energy-state-cli failed: " + string error)
            exit 1
    }
