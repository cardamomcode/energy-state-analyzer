module Energy.Core.Scan

open Fable.Core
open Energy.Core.Esaignore
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Languages.Registry

// decision: the dirent helpers below stay local — they are specific to reading a directory with
// `{ withFileTypes: true }`, which the shared FsPath facade does not model. The plain fs/path
// functions live in FsPath, where Scan and Esaignore already share them.
[<Emit("$0($1, { withFileTypes: true })")>]
let private readDirectory (reader: obj) (directory: Path) : obj[] = nativeOnly

[<Emit("$0.isDirectory()")>]
let private isDirectory (entry: obj) : bool = nativeOnly

[<Emit("$0.isFile()")>]
let private isFile (entry: obj) : bool = nativeOnly

[<Emit("$0.name")>]
let private entryName (entry: obj) : string = nativeOnly

[<Emit("$0.isDirectory()")>]
let private statIsDirectory (stat: obj) : bool = nativeOnly

[<Emit("$0.isFile()")>]
let private statIsFile (stat: obj) : bool = nativeOnly

let private ignoredDirectoryNames =
    Set.ofList
        [ "node_modules"
          ".git"
          "dist"
          "out"
          "build"
          ".next"
          "coverage"
          ".vscode-test" ]

// decision: the walked file set stays a Path list end to end — joinPath's Path results flow
// straight into isIgnored/recursive walks without string round-trips; the string world is only
// reached at the input edge (argv) and in resolvePath's output.
type private IgnoreContext =
    { RootDir: Path; Patterns: string list }

let private isPathIgnored (path: Path) (ignore: IgnoreContext) =
    isIgnored path ignore.RootDir ignore.Patterns

let rec private walkDirectory (directory: Path) ignore results =
    readDirectory readdirSync directory
    |> Array.fold
        (fun files entry ->
            let name = entryName entry
            let fullPath = joinPath directory (Path name)

            if Set.contains name ignoredDirectoryNames || isPathIgnored fullPath ignore then
                files
            elif isDirectory entry then
                walkDirectory fullPath ignore files
            elif isFile entry && resolveLanguageForFile name |> Option.isSome then
                fullPath :: files
            else
                files)
        results

// decision: supports only a trailing `**/*.suffix`-style literal tail; preserving everything after
// the final wildcard keeps compound suffixes such as `.hpp.in` exact without importing a full glob
// engine with divergent semantics.
let private expandGlobLike (pattern: string) ignore =
    let starIndex = pattern.IndexOf('*')
    let prefixEnd = pattern.LastIndexOf(pathSeparator.[0], starIndex)

    let prefixDir =
        if prefixEnd = -1 then
            "."
        else
            pattern.Substring(0, prefixEnd)

    let suffix = pattern.Substring(pattern.LastIndexOf('*') + 1)

    let extension =
        if
            suffix.StartsWith(".", System.StringComparison.Ordinal)
            && not (suffix.Contains "*")
        then
            Some suffix
        else
            None

    if
        not (existsSync (Path prefixDir))
        || not (statIsDirectory (statSync (Path prefixDir)))
    then
        []
    else
        let files = walkDirectory (Path prefixDir) ignore []

        extension
        |> Option.map (fun ext ->
            files
            |> List.filter (fun (Path file) -> file.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase)))
        |> Option.defaultValue files

let resolveSupportedFiles (inputs: string list) (rootDir: string) : Path list =
    let ignore =
        { RootDir = Path rootDir
          Patterns = loadIgnorePatterns rootDir }

    inputs
    |> List.collect (fun input ->
        match input with
        | pattern when pattern.Contains "*" -> expandGlobLike pattern ignore
        | path when not (existsSync (Path path)) -> []
        | path ->
            let stat = statSync (Path path)

            if statIsDirectory stat then
                if isPathIgnored (Path path) ignore then
                    []
                else
                    walkDirectory (Path path) ignore []
            elif
                statIsFile stat
                && resolveLanguageForFile path |> Option.isSome
                && not (isPathIgnored (Path path) ignore)
            then
                [ Path path ]
            else
                [])
    |> List.map resolvePath
    |> Set.ofList
    |> Set.toList
    |> List.sort
