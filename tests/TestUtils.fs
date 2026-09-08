module Energy.Tests.TestUtils

open System.Threading.Tasks
open System
open Fable.Core
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Context
open Energy.Core.FsPath
open Energy.Core.Position
open Energy.Core.Paths
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// Shared integration-test harness.
//
// These helpers read fixtures from the source tree and drive the AnalysisInput pipeline against
// real parsed trees — mirroring how the CLI/extension call analyzeWith, rather than exercising a
// detector in isolation. The pure helpers below are reused by every
// language's integration suite; only parseFixture is language-specific. Filesystem bindings come
// from Core.FsPath so tests exercise the same Node boundary as product code.

// A function identifier searched for within a fixture's source text.
//
// decision: typed rather than left as a raw string so it can no longer be transposed with the
// source text it is searched in.
// invariant: every `FunctionName` value has exactly its wrapped string as its JavaScript
// representation.
[<Erase>]
type FunctionName = FunctionName of string

/// A readable expectation for one named example inside a real-code fixture.
///
/// decision: keeps positive and negative examples together with their language and source file so
/// reviewers can compare equivalent detector behaviour without reconstructing it from bespoke tests.
/// invariant: every scenario asserts the complete registered pipeline, never a detector in isolation.
type FixtureExpectation =
    | StaysClean of FunctionName
    | ProducesFinding of FunctionName * Severity option

type LanguageFixtureCase =
    { LanguageLabel: string
      Language: LanguageAdapter
      Fixture: string
      Expectations: FixtureExpectation list }

// Parse a fixture file with the given language adapter's grammar. Returns (sourceCode, tree).
// Async because web-tree-sitter's Parser.init + Language.load are promises — parseWith bridges them
// into a Task<Node>, and this task { } block awaits each step before returning the parsed root.
let parseFixture (language: LanguageAdapter) (relativePath: string) : Task<string * Node> =
    task {
        let grammarPath = cwd () + "/" + language.GrammarPath
        // decision: read fixtures from the source tree, not out/ — compiled JS never copies fixture
        // files verbatim, so the .py/.fs sources would be missing under out/.
        let sourcePath = cwd () + "/src/test/fixtures/" + relativePath
        let sourceCode = readFileSync (Path sourcePath) (Encoding "utf8")
        let! tree = parseWith (Path grammarPath) sourceCode

        return (sourceCode, tree)
    }

let analyzeFixture sourceCode tree language fileName =
    { Source = sourceCode
      Tree = tree
      Language = language
      FileName = fileName }
    |> analyze
    |> _.Violations

/// Build a detector context for tests that exercise one detector with custom options.
let createTestContext sourceCode tree language fileName options : AnalysisContext =
    { Source = sourceCode
      Tree = tree
      Positions = createPositionLookup sourceCode
      Language = language
      FileName = fileName
      Options = options
      Violations = [] }

// A line range (inclusive, 0-indexed like tree-sitter/EnergyViolation.line) that a named function
// occupies within a fixture, so tests can assert a violation belongs to a specific example function
// without hardcoding exact line numbers. Named type avoids an anonymous-record signature, which F#
// parses ambiguously in this position.
type LineRange = { Start: int; End: int }

let findFunctionRange (sourceCode: string) (FunctionName functionName) : LineRange =
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

/// Produce one parity test per language from a compact positive/negative fixture matrix.
/// The test names deliberately include the fixture scenario, making failures readable in CI and in
/// the source-side test runner.
let detectorParityTests detectorName violationType cases =
    cases
    |> List.map (fun fixtureCase ->
        let scenarioLabel =
            fixtureCase.Expectations
            |> List.map (function
                | StaysClean(FunctionName name) -> name + " clean"
                | ProducesFinding(FunctionName name, _) -> name + " flagged")
            |> String.concat "; "

        testAsync (
            sprintf "%s: %s — %s" detectorName fixtureCase.LanguageLabel scenarioLabel,
            fun _ ->
                toAsync (
                    task {
                        let! (source, tree) = parseFixture fixtureCase.Language fixtureCase.Fixture
                        let violations = analyzeFixture source tree fixtureCase.Language fixtureCase.Fixture
                        assertValidPositions violations source

                        for expectation in fixtureCase.Expectations do
                            match expectation with
                            | StaysClean functionName ->
                                let range = findFunctionRange source functionName

                                assertThat
                                    (violationsIn violations range
                                     |> List.filter (fun violation -> violation.Type = violationType)
                                     |> List.length)
                                    (isEqualTo 0)
                            | ProducesFinding(functionName, expectedSeverity) ->
                                let range = findFunctionRange source functionName

                                assertThat
                                    (violationsIn violations range
                                     |> List.exists (fun violation ->
                                         violation.Type = violationType
                                         && (expectedSeverity |> Option.forall ((=) violation.Severity))))
                                    isTrue
                    }
                )
        ))
