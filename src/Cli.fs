module Energy.Cli

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Energy.Core.TreeSitter
open Energy.Core.Analyze
open Energy.Core.Report
open Energy.Core.Scan
open Energy.Core.Violation
open Energy.Languages.Registry

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = nativeOnly

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = nativeOnly

[<Import("statSync", "node:fs")>]
let private statSync (path: string) : obj = nativeOnly

[<Import("join", "node:path")>]
let private joinPath (left: string) (right: string) : string = nativeOnly

[<Import("relative", "node:path")>]
let private relativePath (fromPath: string) (toPath: string) : string = nativeOnly

[<Emit("$0.isFile()")>]
let private isFile (stat: obj) : bool = nativeOnly

[<Emit("process.argv.slice(2)")>]
let private argv () : string array = nativeOnly

[<Emit("process.cwd()")>]
let private cwd () : string = nativeOnly

[<Emit("new URL('../' + $0, import.meta.url).pathname")>]
let private grammarPath (relative: string) : string = nativeOnly

[<Emit("process.exit($0)")>]
let private exit (code: int) : unit = nativeOnly

[<Emit("console.error($0)")>]
let private error (message: string) : unit = nativeOnly

[<Emit("console.log($0)")>]
let private output (message: string) : unit = nativeOnly

[<Emit("JSON.stringify($0, null, 2)")>]
let private stringify (value: obj) : string = nativeOnly

let private parserCache = System.Collections.Generic.Dictionary<string, Parser>()

let private loadParser (adapter: Energy.Core.LanguageAdapter.LanguageAdapter) : Task<Parser> =
    task {
        match parserCache.TryGetValue adapter.Id with
        | true, parser -> return parser
        | false, _ ->
            do! init parserCtor
            let! grammar = load languageCtor (grammarPath adapter.GrammarPath)
            let parser = makeParser parserCtor
            setLanguage parser grammar |> ignore
            parserCache.Add(adapter.Id, parser)
            return parser
    }

let private analyzeFile filePath sourceText =
    task {
        match resolveLanguageForFile filePath with
        | None -> return []
        | Some adapter ->
            let! parser = loadParser adapter
            let tree = parse parser sourceText |> rootNode
            return analyzeSource sourceText tree adapter filePath
    }

// decision: analyzes scan files sequentially — the shared parser cache avoids repeated grammar
// loads, report order stays deterministic, and this avoids Fable's unavailable Task.WhenAll export.
let rec private analyzeFiles files =
    task {
        match files with
        | [] -> return []
        | file :: rest ->
            let! violations = analyzeFile file (readFileSync file "utf8")
            let! remaining = analyzeFiles rest

            return
                { FilePath = relativePath (cwd ()) file
                  Violations = violations }
                :: remaining
    }

let private violationJson violation =
    let hotspots =
        violation.Hotspots
        |> List.map (fun hotspot -> createObj [ "line" ==> hotspot.Line; "weight" ==> hotspot.Weight ])
        |> List.toArray

    createObj
        [ "line" ==> violation.Line
          "column" ==> violation.Column
          "type" ==> (violationTypeName violation.Type)
          "severity" ==> (severityName violation.Severity)
          "message" ==> violation.Message
          "hotspots" ==> hotspots ]

let private summaryJson summary =
    let files =
        summary.Files
        |> List.map (fun file ->
            createObj
                [ "filePath" ==> file.FilePath
                  "score" ==> file.Score
                  "counts"
                  ==> createObj
                          [ "low" ==> file.Counts.Low
                            "medium" ==> file.Counts.Medium
                            "high" ==> file.Counts.High ]
                  "byType"
                  ==> (file.ByType
                       |> Map.toList
                       |> List.map (fun (key, value) -> key ==> value)
                       |> createObj) ])
        |> List.toArray

    createObj
        [ "files" ==> files
          "totalScore" ==> summary.TotalScore
          "totalCounts"
          ==> createObj
                  [ "low" ==> summary.TotalCounts.Low
                    "medium" ==> summary.TotalCounts.Medium
                    "high" ==> summary.TotalCounts.High ] ]

let private usage () =
    error "Usage: energy-state-cli <file.py|.fs|.fsx|.ts> [thresholds...]"
    error "       energy-state-cli <path...> [--report json|md] [thresholds...]"

// decision: all recognized flags consume exactly one value, preserving the existing simple CLI
// parser and allowing every remaining argument to be treated as a path.
let private parseArguments (arguments: string array) : string list * Map<string, string> =
    let valueFlags =
        Set.ofList
            [ "report"
              "base-ref"
              "medium-nesting"
              "high-nesting"
              "medium-cyclomatic"
              "high-cyclomatic"
              "medium-cognitive"
              "high-cognitive" ]

    let rec loop (index: int) (paths: string list) (flags: Map<string, string>) =
        if index >= arguments.Length then
            paths |> List.rev, flags
        else
            let argument = arguments.[index]

            if
                argument.StartsWith("--")
                && Set.contains (argument.Substring(2)) valueFlags
                && index + 1 < arguments.Length
            then
                loop (index + 2) paths (Map.add (argument.Substring(2)) arguments.[index + 1] flags)
            elif argument.StartsWith("--") then
                loop (index + 1) paths flags
            else
                loop (index + 1) (argument :: paths) flags

    loop 0 [] Map.empty

let runCli () =
    task {
        let paths, flags = parseArguments (argv ())

        let report =
            Map.tryFind "report" flags
            |> Option.defaultValue (if paths.Length = 1 then "json" else "md")

        if paths.IsEmpty then
            usage ()
            exit 2
        elif
            paths.Length = 1
            && existsSync paths.Head
            && isFile (statSync paths.Head)
            && not (Map.containsKey "report" flags)
        then
            let! violations = analyzeFile paths.Head (readFileSync paths.Head "utf8")
            output (stringify (violations |> List.map violationJson |> List.toArray |> box))

            exit (
                if
                    violations
                    |> List.exists (fun violation -> violation.Severity = Medium || violation.Severity = High)
                then
                    1
                else
                    0
            )
        else
            let files = resolveSupportedFiles paths (cwd ())

            let! results = analyzeFiles files
            let summary = summarize results

            output (
                if report = "json" then
                    stringify (summaryJson summary)
                else
                    renderMarkdownReport summary
            )

            exit (if hasBlockingViolations summary.TotalCounts then 1 else 0)
    }
