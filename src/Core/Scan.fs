// # Scan
//
// Scanning expands supplied paths into a deterministic set of supported source files. It prunes built-in
// generated/dependency directories and root-local ignore patterns, handles a deliberately limited wildcard
// form, then normalizes, deduplicates, and sorts paths. Registry.fs owns suffix recognition; unreadable
// directories propagate errors for the host to report.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.Scan

open Fable.Core
open Energy.Core.Esaignore
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Languages.Registry

/// Node bindings for reading a directory's typed entries, kept local to this module.
///
/// decision: the dirent helpers below stay local — they are specific to reading a directory with
/// `{ withFileTypes: true }`, which the shared FsPath facade does not model. The plain fs/path
/// functions live in FsPath, where Scan and Esaignore already share them.
[<Emit("$0($1, { withFileTypes: true })")>]
let private readDirectory (reader: obj) (directory: Path) : obj[] = nativeOnly

/// Test whether a directory entry is a directory before recursively scanning it.
[<Emit("$0.isDirectory()")>]
let private isDirectory (entry: obj) : bool = nativeOnly

/// Test whether a directory entry is a regular file before checking its supported suffix.
[<Emit("$0.isFile()")>]
let private isFile (entry: obj) : bool = nativeOnly

/// Read a directory entry's basename for ignore checks and path construction.
[<Emit("$0.name")>]
let private entryName (entry: obj) : string = nativeOnly

/// Test whether an explicitly supplied input's stat result describes a directory.
[<Emit("$0.isDirectory()")>]
let private statIsDirectory (stat: obj) : bool = nativeOnly

/// Test whether an explicitly supplied input's stat result describes a regular file.
[<Emit("$0.isFile()")>]
let private statIsFile (stat: obj) : bool = nativeOnly

/// Generated-output, dependency, cache, and environment names pruned during recursive directory walks.
let private ignoredDirectoryNames =
    Set.ofList
        [
            "node_modules"
            ".git"
            "bin"
            "obj"
            "dist"
            "out"
            "build"
            "fable-out"
            "fable_modules"
            "fable-tests"
            "spike-js"
            ".next"
            "coverage"
            ".vscode-test"
            "target"
            ".gradle"
            "__pycache__"
            ".venv"
            "venv"
            ".tox"
            ".nox"
            ".pytest_cache"
            ".mypy_cache"
            ".ruff_cache"
        ]

/// Holds the root directory and ignore patterns shared while walking the file tree.
///
/// decision: the walked file set stays a Path list end to end — joinPath's Path results flow
/// straight into isIgnored/recursive walks without string round-trips; the string world is only
/// reached at the input edge (argv) and in resolvePath's output.
type private IgnoreContext =
    { RootDir: Path; Patterns: string list }

/// Apply the scan root's ignore patterns to one candidate path.
let private isPathIgnored (path: Path) (ignore: IgnoreContext) =
    isIgnored path ignore.RootDir ignore.Patterns

/// Walk directory entries recursively, pruning ignored names and paths and collecting supported regular
/// files. Symlink entries are not followed by this dirent walk; filesystem failures propagate.
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

/// Expand a single-level `**/*.suffix` wildcard into an explicit list of matching files.
///
/// decision: supports only a trailing `**/*.suffix`-style literal tail; preserving everything after
/// the final wildcard keeps compound suffixes such as `.hpp.in` exact without importing a full glob
/// engine with divergent semantics.
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

/// Expand files, directories, and limited wildcard inputs using the root's ignore patterns, then return
/// distinct sorted absolute paths. Missing and unsupported inputs contribute nothing; directory-read
/// failures propagate.
let resolveSupportedFiles (inputs: string list) (rootDir: string) : Path list =
    let ignore =
        {
            RootDir = Path rootDir
            Patterns = loadIgnorePatterns rootDir
        }

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
