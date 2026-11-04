module Energy.Cli

open System
open System.Threading.Tasks

open Energy.CliModes
open Energy.CliNode
open Energy.Core.Config
open Energy.Core.FsPath
open Energy.Core.Paths

type private ParsedArguments =
    {
        Paths: string list
        BaseRef: string option
        Report: ReportFormat option
        ConfigFile: string option
        Nesting: int option * int option
        Cyclomatic: int option * int option
        Cognitive: int option * int option
        ParameterCount: int option * int option
        IncludeTestFiles: bool
    }

let private valueFlags =
    Set.ofList
        [
            "base-ref"
            "report"
            "config"
            "medium-nesting"
            "high-nesting"
            "medium-cyclomatic"
            "high-cyclomatic"
            "medium-cognitive"
            "high-cognitive"
            "medium-parameter-count"
            "high-parameter-count"
        ]

/// Classify one CLI token before consuming an optional following value.
type private CliToken =
    | PositionalPath of string
    | BooleanFlag of string
    | ValueFlag of string
    | InvalidOption of string

/// Separate positional paths, known flags, and unknown options.
let private classifyArgument (argument: string) =
    if not (argument.StartsWith("--", StringComparison.Ordinal)) then
        PositionalPath argument
    else
        let name = argument.Substring 2

        if name = "include-test-files" then BooleanFlag name
        elif valueFlags.Contains name then ValueFlag name
        else InvalidOption argument

/// Parse value-consuming CLI flags into a paths list and a flag map.
///
/// decision: recognized flags consume exactly one following value, leaving all other positional
/// arguments untouched so the CLI retains its intentionally small dependency-free parser.
let private parseValues (arguments: string array) : Result<string list * Map<string, string>, string> =
    let rec loop (index: int) (paths: string list) (flags: Map<string, string>) =
        if index >= arguments.Length then
            Ok(List.rev paths, flags)
        else
            match classifyArgument arguments.[index] with
            | PositionalPath path -> loop (index + 1) (path :: paths) flags
            | BooleanFlag name -> loop (index + 1) paths (Map.add name "true" flags)
            | InvalidOption argument -> Error("Unknown option: " + argument)
            | ValueFlag name ->
                match Array.tryItem (index + 1) arguments with
                | Some value when not (value.StartsWith("--", StringComparison.Ordinal)) ->
                    loop (index + 2) paths (Map.add name value flags)
                | _ -> Error("Missing value for --" + name)

    loop 0 [] Map.empty

let private asNumber (flags: Map<string, string>) name =
    flags
    |> Map.tryFind name
    |> Option.bind (fun value ->
        match Int32.TryParse value with
        | true, number -> Some number
        | false, _ -> None)

let private parseArguments arguments : Result<ParsedArguments, string> =
    parseValues arguments
    |> Result.bind (fun (paths, flags) ->
        let invalidNumber =
            valueFlags
            |> Set.filter (fun name -> name <> "base-ref" && name <> "report" && name <> "config")
            |> Seq.tryFind (fun name ->
                Map.tryFind name flags
                |> Option.exists (fun value -> Int32.TryParse value |> fst |> not))

        match invalidNumber with
        | Some name -> Error("Invalid integer for --" + name)
        | None ->
            let report =
                match Map.tryFind "report" flags with
                | Some value -> parseReportFormat value |> Result.map Some
                | None -> Ok None

            report
            |> Result.map (fun selectedReport ->
                {
                    Paths = paths
                    BaseRef = Map.tryFind "base-ref" flags
                    Report = selectedReport
                    ConfigFile = Map.tryFind "config" flags
                    Nesting = asNumber flags "medium-nesting", asNumber flags "high-nesting"
                    Cyclomatic = asNumber flags "medium-cyclomatic", asNumber flags "high-cyclomatic"
                    Cognitive = asNumber flags "medium-cognitive", asNumber flags "high-cognitive"
                    ParameterCount = asNumber flags "medium-parameter-count", asNumber flags "high-parameter-count"
                    IncludeTestFiles = Map.containsKey "include-test-files" flags
                }))

/// Merge CLI threshold overrides onto the config-resolved base options.
///
/// decision: the CLI reads .esaconfig.json by default (searching up from the current directory), or an
/// explicit path via --config; threshold flags then override whatever that file set. This is why the
/// defaults now come from Core.Config — they are the same values a project's config overlays onto.
let private buildThresholds parsed : Result<AnalyzeOptions, string> =
    let loaded =
        match parsed.ConfigFile with
        | Some configPath -> loadAnalyzeOptionsFromConfigPath (Path configPath)
        | None -> loadAnalyzeOptions (Path(cwd ()))

    loaded
    |> Result.mapError configErrorMessage
    |> Result.map (fun baseOptions ->
        { baseOptions with
            // decision: the allowlists, enabled flags, and min-duplicates all come from baseOptions, which
            // already merged .esaconfig.json over the Core.Config defaults; only the --include-test-files flag
            // overrides them, so a project's config can no longer be silently discarded by the CLI.
            MagicNumber =
                { baseOptions.MagicNumber with
                    IncludeTestFiles = parsed.IncludeTestFiles
                }
            MagicString =
                { baseOptions.MagicString with
                    IncludeTestFiles = parsed.IncludeTestFiles
                }
            OpaqueBoolean =
                { baseOptions.OpaqueBoolean with
                    IncludeTestFiles = parsed.IncludeTestFiles
                }
            Nesting =
                { baseOptions.Nesting with
                    MediumThreshold = Option.defaultValue baseOptions.Nesting.MediumThreshold (fst parsed.Nesting)
                    HighThreshold = Option.defaultValue baseOptions.Nesting.HighThreshold (snd parsed.Nesting)
                }
            Cyclomatic =
                { baseOptions.Cyclomatic with
                    MediumThreshold =
                        Option.defaultValue baseOptions.Cyclomatic.MediumThreshold (fst parsed.Cyclomatic)
                    HighThreshold = Option.defaultValue baseOptions.Cyclomatic.HighThreshold (snd parsed.Cyclomatic)
                }
            Cognitive =
                { baseOptions.Cognitive with
                    MediumThreshold = Option.defaultValue baseOptions.Cognitive.MediumThreshold (fst parsed.Cognitive)
                    HighThreshold = Option.defaultValue baseOptions.Cognitive.HighThreshold (snd parsed.Cognitive)
                }
            ParameterCount =
                { baseOptions.ParameterCount with
                    MediumThreshold =
                        Option.defaultValue baseOptions.ParameterCount.MediumThreshold (fst parsed.ParameterCount)
                    HighThreshold =
                        Option.defaultValue baseOptions.ParameterCount.HighThreshold (snd parsed.ParameterCount)
                }
        })

/// Reject explicit paths that cannot contribute a supported input file.
let private validatePaths (paths: string list) =
    paths
    |> List.tryPick (fun path ->
        if path.Contains "*" then
            None
        elif not (existsSync (Path path)) then
            Some("Input path not found: " + path)
        elif
            isFile (statSync (Path path))
            && (Energy.Languages.Registry.resolveLanguageForFile path |> Option.isNone)
        then
            Some("Unsupported input file: " + path)
        else
            None)
    |> function
        | Some message -> Error message
        | None -> Ok()

let runCli () : Task<unit> =
    task {
        try
            let request =
                parseArguments (argv ())
                |> Result.bind (fun parsed ->
                    validatePaths parsed.Paths
                    |> Result.bind (fun () ->
                        buildThresholds parsed |> Result.map (fun thresholds -> parsed, thresholds)))

            match request with
            | Error message ->
                Energy.CliNode.error ("energy-state-cli: " + message)
                exit 2
            | Ok(parsed, thresholds) ->
                // decision: scan defaults to compact JSON; diff keeps its Markdown default.
                let report =
                    parsed.Report
                    |> Option.defaultValue (if parsed.BaseRef.IsSome then Markdown else Json)

                match parsed.BaseRef, parsed.Paths with
                | Some baseRef, _ -> do! runDiff baseRef parsed.Paths thresholds report
                | None, [] ->
                    printUsage ()
                    exit 2
                | None, _ -> do! runScan parsed.Paths thresholds report
        with error ->
            Energy.CliNode.error ("energy-state-cli failed: " + string<exn> error)
            exit 1
    }
