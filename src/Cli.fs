module Energy.Cli

open System
open System.Threading.Tasks

open Energy.CliModes
open Energy.CliNode
open Energy.Core.Analyze
open Energy.Core.Detectors.Cognitive
open Energy.Core.Detectors.Cyclomatic
open Energy.Core.Detectors.Nesting

type private ParsedArguments =
    { Paths: string list
      BaseRef: string option
      Report: ReportFormat option
      Nesting: int option * int option
      Cyclomatic: int option * int option
      Cognitive: int option * int option }

let private valueFlags =
    Set.ofList
        [ "base-ref"
          "report"
          "medium-nesting"
          "high-nesting"
          "medium-cyclomatic"
          "high-cyclomatic"
          "medium-cognitive"
          "high-cognitive" ]

// decision: recognized flags consume exactly one following value, leaving all other positional
// arguments untouched so the CLI retains its intentionally small dependency-free parser.
let private parseValues (arguments: string array) : string list * Map<string, string> =
    let rec loop (index: int) (paths: string list) (flags: Map<string, string>) =
        if index >= arguments.Length then
            List.rev paths, flags
        else
            let argument = arguments.[index]

            if argument.StartsWith "--" && valueFlags.Contains(argument.Substring 2) && index + 1 < arguments.Length then
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
      Nesting = asNumber flags "medium-nesting", asNumber flags "high-nesting"
      Cyclomatic = asNumber flags "medium-cyclomatic", asNumber flags "high-cyclomatic"
      Cognitive = asNumber flags "medium-cognitive", asNumber flags "high-cognitive" }

let private thresholdOverride (defaultMedium, defaultHigh) constructor (medium, high) =
    match medium, high with
    | None, None -> None
    | _ ->
        Some(
            constructor
                (Option.defaultValue defaultMedium medium)
                (Option.defaultValue defaultHigh high)
        )

let private buildThresholds parsed =
    { defaultThresholds with
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
            let report = parsed.Report |> Option.defaultValue (if parsed.BaseRef.IsSome || parsed.Paths.Length <> 1 then "md" else "json")

            match parsed.BaseRef, parsed.Paths with
            | Some baseRef, _ -> do! runDiff baseRef parsed.Paths thresholds report
            | None, [] ->
                printUsage ()
                exit 2
            | None, [ path ] when existsSync path && isFile (statSync path) && parsed.Report.IsNone ->
                do! runLegacySingleFile path thresholds
            | None, _ -> do! runScan parsed.Paths thresholds report
        with error ->
            Energy.CliNode.error ("energy-state-cli failed: " + string error)
            exit 1
    }
