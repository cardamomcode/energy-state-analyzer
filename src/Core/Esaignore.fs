module Energy.Core.Esaignore

open Fable.Core

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = nativeOnly

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = nativeOnly

[<Import("join", "node:path")>]
let private joinPath (left: string) (right: string) : string = nativeOnly

[<Import("relative", "node:path")>]
let private relativePath (fromPath: string) (toPath: string) : string = nativeOnly

[<Import("basename", "node:path")>]
let private basename (path: string) : string = nativeOnly

[<Import("sep", "node:path")>]
let private pathSeparator: string = nativeOnly

let esaignoreFileName = ".esaignore"

// decision: deliberately supports only literal paths and single-segment basename globs; matching
// scan's intentionally small glob surface avoids silently claiming full gitignore semantics.
let loadIgnorePatterns (rootDir: string) : string list =
    let ignorePath = joinPath rootDir esaignoreFileName

    if not (existsSync ignorePath) then
        []
    else
        (readFileSync ignorePath "utf8").Split('\n')
        |> Array.map _.Trim()
        |> Array.filter (fun line -> line <> "" && not (line.StartsWith "#"))
        |> Array.map (fun line -> line.TrimEnd('/'))
        |> Array.toList

let private matchesLiteralPattern (relative: string) (pattern: string) =
    if pattern.Contains "/" then
        relative = pattern || relative.StartsWith(pattern + "/")
    else
        relative.Split('/') |> Array.contains pattern

let private matchesBasenameGlob (pattern: string) (name: string) =
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

let isIgnored (absolutePath: string) (rootDir: string) (patterns: string list) =
    if patterns.IsEmpty then
        false
    else
        let relative =
            relativePath rootDir absolutePath
            |> fun path -> path.Replace(pathSeparator, "/")

        let name = basename absolutePath

        patterns
        |> List.exists (fun pattern ->
            if pattern.Contains "*" then
                matchesBasenameGlob pattern name
            else
                matchesLiteralPattern relative pattern)
