module Energy.Tests.CognitiveRuleTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter
open Energy.Core.Config
open Energy.Core.Analyze
open Energy.Core.Position
open Energy.Core.Violation
open Energy.Core.Detectors.Cognitive
open Energy.Tests.TestUtils

/// Locate a named example by its declaration line, excluding references in function bodies.
let rec private findFunction (language: LanguageAdapter) name node =
    if
        language.IsFunctionDefinition node
        && (nodeText node).Split('\n').[0].Contains(" " + name)
    then
        Some node
    else
        nodeNamedChildren node |> List.tryPick (findFunction language name)

/// Run the registered pipeline with a cognitive threshold at the requested boundary.
let private findings language source tree fixture threshold =
    let options =
        { defaultAnalyzeOptions with
            Cognitive =
                { defaultCognitiveThresholds with
                    MediumThreshold = threshold
                    HighThreshold = threshold + 1 } }

    analyzeWith
        options
        { Source = source
          Tree = tree
          Language = language
          FileName = fixture }
    |> _.Violations
    |> List.filter (fun violation -> violation.Type = Cognitive)

/// Immutable parsed input and pipeline results shared by one fixture's named rule cases.
type private RuleFixture =
    { Tree: Node
      Positions: PositionLookup
      FindingsAtThreshold: int -> EnergyViolation list }

/// Parse each fixture once and run its complete pipeline once per distinct threshold.
///
/// decision: share the pending task as well as its result because Quill starts named cases in
/// parallel; repeated grammar loads and whole-file scans otherwise exhaust CI's test timeout.
/// invariant: every case retains its assertions and each cached result belongs to one fixture
/// and one threshold; the syntax tree is read-only for the lifetime of these tests.
let private prepareFixture language fixture =
    lazy
        (task {
            let! source, tree = parseFixture language fixture
            let mutable results = Map.empty

            let findingsAtThreshold threshold =
                match Map.tryFind threshold results with
                | Some cached -> cached
                | None ->
                    let computed = findings language source tree fixture threshold
                    results <- Map.add threshold computed results
                    computed

            return
                { Tree = tree
                  Positions = createPositionLookup source
                  FindingsAtThreshold = findingsAtThreshold }
        })

/// Check exact scores, heatmap accounting, and both sides of the reporting threshold.
let private ruleTest language (prepared: System.Lazy<_>) (name, expected) =
    testAsync (
        sprintf "%s: %s scores %d" language.Id name expected,
        fun _ ->
            toAsync (
                task {
                    let! (fixture: RuleFixture) = prepared.Value
                    assertThat (nodeHasError fixture.Tree) (isEqualTo false)
                    let fn = findFunction language name fixture.Tree |> Option.get
                    let positions = fixture.Positions
                    let line = (positions.toPosition (nodeStartIndex fn)).Line
                    let atFunction = List.filter (fun violation -> violation.Line = line)
                    assertThat (cognitiveScoreOf language fn) (isEqualTo expected)
                    let hotspots = findCognitiveHotspots language fn positions
                    assertThat (hotspots |> List.sumBy _.Weight) (isEqualTo expected)

                    if name = "multilineRuns" then
                        assertThat (hotspots |> List.map _.Line) (isEqualTo [ line + 2; line + 3 ])

                    assertThat (hotspots |> List.forall (fun point -> point.Weight > 0)) (isEqualTo true)

                    assertThat (fixture.FindingsAtThreshold expected |> atFunction |> List.length) (isEqualTo 0)

                    if expected > 0 then
                        let reported = fixture.FindingsAtThreshold(expected - 1) |> atFunction
                        assertThat reported.Length (isEqualTo 1)
                        assertThat reported.Head.Severity (isEqualTo Medium)
                        assertThat reported.Head.Hotspots (isEqualTo hotspots)
                        assertThat (reported.Head.Message.Contains(sprintf ": %d." expected)) (isEqualTo true)

                    if expected > 1 then
                        let high = fixture.FindingsAtThreshold(expected - 2) |> atFunction
                        assertThat high.Head.Severity (isEqualTo High)
                }
            )
    )

/// Named positive and negative rule scenarios shared with the detector fixture matrix.
let fixtureTests =
    CognitiveRuleCases.cases
    |> List.collect (fun (language, fixture, cases) ->
        let prepared = prepareFixture language fixture
        cases |> List.map (ruleTest language prepared))
