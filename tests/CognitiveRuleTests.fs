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

/// Check exact scores, heatmap accounting, and both sides of the reporting threshold.
let private ruleTest language fixture (name, expected) =
    testAsync (
        sprintf "%s: %s scores %d" language.Id name expected,
        fun _ ->
            toAsync (
                task {
                    let! source, tree = parseFixture language fixture
                    assertThat (nodeHasError tree) (isEqualTo false)
                    let fn = findFunction language name tree |> Option.get
                    let positions = createPositionLookup source
                    let line = (positions.toPosition (nodeStartIndex fn)).Line
                    let atFunction = List.filter (fun violation -> violation.Line = line)
                    assertThat (cognitiveScoreOf language fn) (isEqualTo expected)
                    let hotspots = findCognitiveHotspots language fn positions
                    assertThat (hotspots |> List.sumBy _.Weight) (isEqualTo expected)

                    if name = "multilineRuns" then
                        assertThat (hotspots |> List.map _.Line) (isEqualTo [ line + 2; line + 3 ])

                    assertThat (hotspots |> List.forall (fun point -> point.Weight > 0)) (isEqualTo true)

                    assertThat
                        (findings language source tree fixture expected |> atFunction |> List.length)
                        (isEqualTo 0)

                    if expected > 0 then
                        let reported = findings language source tree fixture (expected - 1) |> atFunction
                        assertThat reported.Length (isEqualTo 1)
                        assertThat reported.Head.Severity (isEqualTo Medium)
                        assertThat reported.Head.Hotspots (isEqualTo hotspots)
                        assertThat (reported.Head.Message.Contains(sprintf ": %d." expected)) (isEqualTo true)

                    if expected > 1 then
                        let high = findings language source tree fixture (expected - 2) |> atFunction
                        assertThat high.Head.Severity (isEqualTo High)
                }
            )
    )

/// Named positive and negative rule scenarios shared with the detector fixture matrix.
let fixtureTests =
    CognitiveRuleCases.cases
    |> List.collect (fun (language, fixture, cases) -> cases |> List.map (ruleTest language fixture))
