// # TestUtils
//
// The integration harness loads real grammar WASMs and fixture source, caches pending grammar/parse tasks,
// and runs the registered analyzer pipeline. Named scenarios are located by a textual fixture convention
// rather than absolute line numbers. Cached fixture trees remain live for the test process; SpikeTests.fs
// checks tree release and ParsedAnalysisTests.fs checks typed parse failure.
//
// Reading path: [Source guide](../docs/source-guide.md).

module Energy.Tests.TestUtils

open System.Threading.Tasks
open System
open Fable.Core
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Core.TreeSitter

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
/// Fixture function-name text kept distinct from full source text at assertion call sites.
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

/// One language/grammar and fixture path with named clean or flagged scenarios exercised through the full
/// pipeline.
type LanguageFixtureCase =
    {
        LanguageLabel: string
        Language: Energy.Core.LanguageAdapter.LanguageAdapter
        Fixture: string
        Expectations: FixtureExpectation list
    }

/// Pending grammar loads shared by every fixture that uses the same grammar WASM.
let private grammarLoads =
    System.Collections.Generic.Dictionary<string, Task<Grammar>>()

/// Pending parses shared by tests that use the same immutable source fixture and grammar.
let private fixtureParses =
    System.Collections.Generic.Dictionary<string, Task<string * Node>>()

/// Load one grammar instance per WASM path, including when tests request it concurrently.
///
/// decision: caches the pending task, not only its result, because Quill starts async cases in
/// parallel and simultaneous cache misses would otherwise instantiate the same WASM repeatedly.
let private getOrLoadGrammar grammarPath =
    match grammarLoads.TryGetValue grammarPath with
    | true, pending -> pending
    | false, _ ->
        let pending =
            task {
                do! init ()
                return! load languageCtor (Path grammarPath)
            }

        grammarLoads.Add(grammarPath, pending)
        pending

/// Parse a fixture once and share its pending task and immutable syntax tree across test cases.
///
/// decision: creates a fresh parser for each distinct fixture while sharing its loaded grammar;
/// parser state does not cross fixture parses, and read-only analyses reuse one fixture tree.
let parseFixture (language: Energy.Core.LanguageAdapter.LanguageAdapter) (relativePath: string) : Task<string * Node> =
    let grammarPath = cwd () + "/" + language.GrammarPath
    // decision: read fixtures from the source tree, not out/ — compiled JS never copies fixture
    // files verbatim, so the .py/.fs sources would be missing under out/.
    let sourcePath = cwd () + "/src/test/fixtures/" + relativePath
    let cacheKey = grammarPath + "\u001f" + sourcePath

    match fixtureParses.TryGetValue cacheKey with
    | true, pending -> pending
    | false, _ ->
        let pending =
            task {
                let sourceCode = readFileSync (Path sourcePath) (Encoding "utf8")
                let! grammar = getOrLoadGrammar grammarPath
                let parser = makeParser parserCtor
                setLanguage parser grammar |> ignore
                let tree = parse parser sourceCode
                return (sourceCode, rootNode tree)
            }

        fixtureParses.Add(cacheKey, pending)
        pending

/// Analyze an already parsed fixture with default options and return all pipeline findings, including
/// suppression notes.
let analyzeFixture sourceCode tree language fileName =
    {
        Source = sourceCode
        Tree = tree
        Language = language
        FileName = fileName
    }
    |> analyze
    |> _.Violations

/// Build a detector context for tests that exercise one detector with custom options.
let createTestContext sourceCode tree language fileName options : Energy.Core.Context.AnalysisContext =
    {
        Source = sourceCode
        Tree = tree
        Positions = Energy.Core.Position.createPositionLookup sourceCode
        Language = language
        FileName = fileName
        Options = options
        Violations = []
    }

// A line range (inclusive, 0-indexed like tree-sitter/EnergyViolation.line) that a named function
// occupies within a fixture, so tests can assert a violation belongs to a specific example function
// without hardcoding exact line numbers. Named type avoids an anonymous-record signature, which F#
// parses ambiguously in this position.
/// Inclusive zero-based fixture line interval used to associate findings with a named scenario.
type LineRange = { Start: int; End: int }

/// Locate the first line containing a fixture function name and end before the next nonindented nonempty
/// line, or at EOF. Missing names fail the test; fixtures must follow this textual boundary convention.
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
/// Keep findings whose zero-based line lies inside the inclusive fixture interval, preserving their order.
let violationsIn (violations: EnergyViolation list) (range: LineRange) : EnergyViolation list =
    violations
    |> List.filter (fun v -> v.Line >= range.Start && v.Line <= range.End)

/// Assert in-range lines, nonnegative columns, and nonempty F# debug formatting for every finding. This
/// does not check column upper bounds or JSON serialization.
let assertValidPositions (violations: EnergyViolation list) (sourceCode: string) : unit =
    let lineCount = sourceCode.Split('\n').Length

    for v in violations do
        assertThat v.Line (isGreaterOrEqual 0)
        assertThat v.Line (isLessThan lineCount)
        assertThat v.Column (isGreaterOrEqual 0)
        // F# debug formatting is a smoke check only; it does not establish JSON serializability.
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
