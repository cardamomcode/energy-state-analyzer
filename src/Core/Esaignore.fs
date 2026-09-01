module Energy.Core.Esaignore

open Fable.Core
open Energy.Core.FsPath
open Energy.Core.Paths

// A `.esaignore` pattern: a literal path or a single-segment basename glob.
//
// decision: typed rather than left as a raw string so a pattern can no longer be transposed with
// the path it is matched against.
// invariant: every `IgnorePattern` value has exactly its wrapped string as its JavaScript
// representation.
[<Erase>]
type IgnorePattern = IgnorePattern of string

let esaignoreFileName = ".esaignore"

// decision: deliberately supports only literal paths and single-segment basename globs; matching
// scan's intentionally small glob surface avoids silently claiming full gitignore semantics.
let loadIgnorePatterns (rootDir: string) : string list =
    let ignorePath = joinPath (Path rootDir) (Path esaignoreFileName)

    if not (existsSync ignorePath) then
        []
    else
        (readFileSync ignorePath (Encoding "utf8")).Split('\n')
        |> Array.map _.Trim()
        |> Array.filter (fun line -> line <> "" && not (line.StartsWith "#"))
        |> Array.map (fun line -> line.TrimEnd('/'))
        |> Array.toList

let private matchesLiteralPattern (relative: string) (IgnorePattern pattern) =
    if pattern.Contains "/" then
        relative = pattern || relative.StartsWith(pattern + "/")
    else
        relative.Split('/') |> Array.contains pattern

let private matchesBasenameGlob (IgnorePattern pattern) (name: string) =
    let pieces = pattern.Split('*')

    let rec loop (remaining: string list) (position: int) =
        match remaining with
        | [] -> position = name.Length
        | first :: rest when position = 0 ->
            if name.StartsWith first then
                loop rest first.Length
            else
                false
        | part :: rest ->
            let index = name.IndexOf(part, position, System.StringComparison.Ordinal)
            if index < 0 then false else loop rest (index + part.Length)

    loop (pieces |> Array.toList) 0

// decision: keeps its Path values unwrapped — they flow straight into the fs/path bindings
// (relativePath/basename) rather than round-tripping through a destructured string.
let isIgnored (absolutePath: Path) (rootDir: Path) (patterns: string list) =
    if patterns.IsEmpty then
        false
    else
        let relative =
            relativePath rootDir absolutePath
            |> fun path -> path.Replace(pathSeparator, "/")

        let name = basename absolutePath

        patterns
        |> List.exists (fun pattern ->
            let typedPattern = IgnorePattern pattern

            if pattern.Contains "*" then
                matchesBasenameGlob typedPattern name
            else
                matchesLiteralPattern relative typedPattern)
