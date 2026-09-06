module Energy.Core.ArchitecturePolicy

open Fable.Core
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Core.ArchitectureModel

let architectureConfigFileName = ".esa-architecture.json"

[<Emit("$0[$1]")>]
let private getProp (value: obj) (key: string) : obj = nativeOnly

[<Emit("$0 == null")>]
let private isNullOrUndefined (value: obj) : bool = nativeOnly

[<Emit("JSON.parse($0)")>]
let private jsonParse (text: string) : obj = nativeOnly

let private field (parent: obj) key =
    let value = getProp parent key
    if isNullOrUndefined value then None else Some value

let private readStrings (parent: obj) key =
    field parent key
    |> Option.map (fun value -> unbox<string array> value |> Array.toList)
    |> Option.defaultValue []

let private normalize (value: string) = value.Replace("\\", "/").TrimEnd('/')

let private parsePolicy (raw: obj) : Result<ArchitecturePolicy, string> =
    match field raw "zones", field raw "forbiddenDependencies" with
    | Some zoneValues, Some dependencyValues ->
        let zones =
            unbox<obj array> zoneValues
            |> Array.toList
            |> List.map (fun zone ->
                { Name = field zone "name" |> Option.map unbox<string> |> Option.defaultValue ""
                  Files = readStrings zone "files" |> List.map normalize
                  ImportPrefixes = readStrings zone "importPrefixes" |> List.map normalize })

        let dependencies =
            unbox<obj array> dependencyValues
            |> Array.toList
            |> List.map (fun dependency ->
                { From = field dependency "from" |> Option.map unbox<string> |> Option.defaultValue ""
                  To = field dependency "to" |> Option.map unbox<string> |> Option.defaultValue "" })

        let names = zones |> List.map _.Name

        let invalidZone zone =
            zone.Name.Trim() = "" || zone.Files.IsEmpty || zone.ImportPrefixes.IsEmpty

        if zones.IsEmpty || List.exists invalidZone zones then
            Error "zones must contain name, files, and importPrefixes"
        elif names |> Set.ofList |> Set.count <> names.Length then
            Error "zone names must be unique"
        elif
            dependencies
            |> List.exists (fun dependency ->
                not (List.contains dependency.From names && List.contains dependency.To names))
        then
            Error "every forbidden dependency must name configured zones"
        else
            Ok
                { Zones = zones
                  ForbiddenDependencies = dependencies }
    | _ -> Error "expected zones and forbiddenDependencies arrays"

/// Parse a policy document before it is associated with a repository root.
let parsePolicyJson (text: string) : Result<ArchitecturePolicy, string> =
    try
        jsonParse text |> parsePolicy
    with _ ->
        Error "could not parse architecture policy JSON"

/// Load the required repository policy from the audit root.
// decision: policy loading fails closed because silently skipping a malformed architecture policy
// would make an explicit CI boundary gate appear to have protected a repository when it did not.
let loadPolicy (root: Path) : Result<ArchitecturePolicy, string> =
    let path = joinPath root (Path architectureConfigFileName)

    if not (existsSync path) then
        Error("missing " + architectureConfigFileName)
    else
        readFileSync path (Encoding "utf8")
        |> parsePolicyJson
        |> Result.mapError (fun _ -> "could not parse " + architectureConfigFileName)
