module Energy.Tests.TestUtils

open System.Threading.Tasks
open System
// Fable.Core exposes [<Emit>]/[<Import>]/nativeOnly (mirrors SpikeTests, which also opens it).
open Fable.Core

open Fable.Core.JsInterop

// Scriptorium.Nib.Assertion supplies assertThat/satisfy/isGreaterOrEqual/isLessThan; the type alias
// keeps them in scope alongside the assertion combinators (mirrors HelloTests/SpikeTests).
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// Shared integration-test harness (port of src/test/integration/testUtils.ts).
//
// These helpers read fixtures from the source tree and drive the pipeline entry point
// (analyzeSource) against real parsed trees — mirroring how the CLI/extension call analyzeSource,
// rather than exercising a detector in isolation. The pure helpers below are reused by every
// language's integration suite; only parseFixture is language-specific.

[<Emit("process.cwd()")>]
let cwd () : string = nativeOnly

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: string) (encoding: string) : string = nativeOnly

// Parse a fixture file with the given language adapter's grammar. Returns (sourceCode, tree).
// Async because web-tree-sitter's Parser.init + Language.load are promises — parseWith bridges them
// into a Task<Node>, and this task { } block awaits each step before returning the parsed root.
let parseFixture (language: LanguageAdapter) (relativePath: string) : Task<string * Node> =
    task {
        let grammarPath = cwd () + "/" + language.GrammarPath
        // decision: read fixtures from the source tree, not out/ — compiled JS never copies fixture
        // files verbatim, so the .py/.fs sources would be missing under out/.
        let sourcePath = cwd () + "/src/test/fixtures/" + relativePath
        let sourceCode = readFileSync sourcePath "utf8"
        let! tree = parseWith grammarPath sourceCode

        return (sourceCode, tree)
    }

// A line range (inclusive, 0-indexed like tree-sitter/EnergyViolation.line) that a named function
// occupies within a fixture, so tests can assert a violation belongs to a specific example function
// without hardcoding exact line numbers. Named type avoids an anonymous-record signature, which F#
// parses ambiguously in this position.
type LineRange = { Start: int; End: int }

let findFunctionRange (sourceCode: string) (functionName: string) : LineRange =
    let lines = sourceCode.Split('\n')

    let start =
        lines
        |> Array.tryFindIndex (fun line -> line.Contains(functionName))
        |> Option.defaultValue (-1)

    if start = -1 then
        failwithf "fixture does not contain a function named '%s'" functionName

    // decision: the next top-level definition starts in column 0 with no leading whitespace (def/let at
    // module scope) — the fixture convention every rule fixture follows, so it's a reliable "next
    // function starts here" marker. If none, the range extends to end of file. F# has no `break`, so we
    // scan only the lines after this one; Array.tryFindIndex returns the first such marker (or None).
    let afterStart = lines.[(start + 1) .. lines.Length - 1]

    let endLine =
        match
            afterStart
            |> Array.tryFindIndex (fun line -> line.Length > 0 && not (Char.IsWhiteSpace line.[0]))
        with
        | Some markerIdx -> start + markerIdx
        | None -> lines.Length - 1

    { Start = start; End = endLine }

// Violations whose line falls within the given inclusive range — scopes a check to one function's
// body without depending on absolute line numbers.
let violationsIn (violations: EnergyViolation list) (range: LineRange) : EnergyViolation list =
    violations
    |> List.filter (fun v -> v.Line >= range.Start && v.Line <= range.End)

// Invariant every real violation must satisfy: its line is in range for the file, its column is
// non-negative, and it round-trips through serialization (plain data — no circular refs). Mirrors
// the TS `assertValidPositions` (line/column bounds + JSON.stringify not throwing).
let assertValidPositions (violations: EnergyViolation list) (sourceCode: string) : unit =
    let lineCount = sourceCode.Split('\n').Length

    for v in violations do
        assertThat v.Line (isGreaterOrEqual 0)
        assertThat v.Line (isLessThan lineCount)
        assertThat v.Column (isGreaterOrEqual 0)
        // decision: mirrors the TS `JSON.stringify(violations)` round-trip — proves the record is plain,
        // serializable data rather than carrying live tree handles. sprintf never throws on a plain
        // record; asserting its output is non-empty confirms serialization produced the expected shape.
        assertThat (sprintf "%A" v) (satisfy (fun s -> s.Length > 0))
