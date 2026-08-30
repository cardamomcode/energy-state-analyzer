module Energy.Core.Scan

open Fable.Core
open Energy.Core.Esaignore
open Energy.Core.FsPath
open Energy.Languages.Registry

// decision: the dirent helpers below stay local — they are specific to reading a directory with
// `{ withFileTypes: true }`, which the shared FsPath facade does not model. The plain fs/path
// functions live in FsPath, where Scan and Esaignore already share them.
[<Emit("$0($1, { withFileTypes: true })")>]
let private readDirectory (reader: obj) (directory: string) : obj[] = nativeOnly

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

type private IgnoreContext =
    { RootDir: string
      Patterns: string list }

let private isPathIgnored path ignore =
    isIgnored path ignore.RootDir ignore.Patterns

let rec private walkDirectory directory ignore results =
    readDirectory readdirSync directory
    |> Array.fold
        (fun files entry ->
            let name = entryName entry
            let fullPath = joinPath directory name

            if Set.contains name ignoredDirectoryNames || isPathIgnored fullPath ignore then
                files
            elif isDirectory entry then
                walkDirectory fullPath ignore files
            elif isFile entry && resolveLanguageForFile name |> Option.isSome then
                fullPath :: files
            else
                files)
        results

// decision: supports only a trailing `**/*.ext`-style suffix; this intentionally remains a
// lightweight CLI convenience rather than importing a full glob engine with divergent semantics.
let private expandGlobLike (pattern: string) ignore =
    let starIndex = pattern.IndexOf('*')
    let prefixEnd = pattern.LastIndexOf(pathSeparator.[0], starIndex)

    let prefixDir =
        if prefixEnd = -1 then
            "."
        else
            pattern.Substring(0, prefixEnd)

    let suffix = pattern.Substring(pattern.LastIndexOf('.'))

    let extension =
        if suffix.StartsWith "." && not (suffix.Contains "*") then
            Some suffix
        else
            None

    if not (existsSync prefixDir) || not (statIsDirectory (statSync prefixDir)) then
        []
    else
        let files = walkDirectory prefixDir ignore []

        extension
        |> Option.map (fun ext ->
            files
            |> List.filter (fun file -> file.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase)))
        |> Option.defaultValue files

let resolveSupportedFiles (inputs: string list) (rootDir: string) =
    let ignore =
        { RootDir = rootDir
          Patterns = loadIgnorePatterns rootDir }

    inputs
    |> List.collect (fun input ->
        match input with
        | pattern when pattern.Contains "*" -> expandGlobLike pattern ignore
        | path when not (existsSync path) -> []
        | path ->
            let stat = statSync path

            if statIsDirectory stat then
                if isPathIgnored path ignore then
                    []
                else
                    walkDirectory path ignore []
            elif
                statIsFile stat
                && resolveLanguageForFile path |> Option.isSome
                && not (isPathIgnored path ignore)
            then
                [ path ]
            else
                [])
    |> List.map resolvePath
    |> Set.ofList
    |> Set.toList
    |> List.sort
