module Energy.Core.Scan

open Fable.Core
open Energy.Core.Esaignore
open Energy.Languages.Registry

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = nativeOnly

[<Import("statSync", "node:fs")>]
let private statSync (path: string) : obj = nativeOnly

[<Import("readdirSync", "node:fs")>]
let private readdirSync: obj = nativeOnly

[<Import("join", "node:path")>]
let private joinPath (left: string) (right: string) : string = nativeOnly

[<Import("resolve", "node:path")>]
let private resolvePath (path: string) : string = nativeOnly

[<Import("sep", "node:path")>]
let private pathSeparator: string = nativeOnly

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
        if input.Contains "*" then
            expandGlobLike input ignore
        elif not (existsSync input) then
            []
        else
            let stat = statSync input

            if statIsDirectory stat then
                if isPathIgnored input ignore then
                    []
                else
                    walkDirectory input ignore []
            elif
                statIsFile stat
                && resolveLanguageForFile input |> Option.isSome
                && not (isPathIgnored input ignore)
            then
                [ input ]
            else
                [])
    |> List.map resolvePath
    |> Set.ofList
    |> Set.toList
    |> List.sort
