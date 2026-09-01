module Energy.Tests.CoherenceTests

open System.Threading.Tasks

open Fable.Core.JsInterop

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.LanguageAdapter
open Energy.Core.Analyze
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin
open Energy.Languages.CPlusPlus
open Energy.Tests.TestUtils

// decision: coherence is a whole-file metric (function count, large-function count, import count),
// unlike every other detector here — it can't be exercised with a "clean version + flagged version in
// one file" fixture, so each scenario gets its own file instead. This mirrors coherence.test.ts: two
// language blocks, one over every supported language and one (class-relatedness only) over those
// that have a real class construct — F# has no classDefinitionNodeTypes, so it's excluded rather than
// given empty fixtures.

let private coherenceHits (violations: EnergyViolation list) : EnergyViolation list =
    violations |> List.filter (fun v -> v.Type = Coherence)

let private hitsWithMessage (substrings: string list) (violations: EnergyViolation list) : EnergyViolation list =
    violations
    |> List.filter (fun v -> substrings |> List.exists (fun s -> v.Message.Contains s))

// "flagged" scenarios — assert at least one matching coherence violation is present.
let private assertManyLargeFunctions (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "exceed" ] vs |> List.length > 0) isTrue

let private assertManyImports (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "Import sprawl" ] vs |> List.length > 0) isTrue

// "stays quiet" scenarios — assert no matching coherence violation is present.
let private assertNarrowImportsQuiet (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "Import sprawl" ] vs |> List.length) (isEqualTo 0)

let private assertCleanQuiet (_src: string) (vs: EnergyViolation list) =
    assertThat (coherenceHits vs |> List.length) (isEqualTo 0)

let private assertTypeCohesiveQuiet (_src: string) (vs: EnergyViolation list) =
    assertThat (coherenceHits vs |> List.length) (isEqualTo 0)

let private assertRelatedClassesQuiet (_src: string) (vs: EnergyViolation list) =
    assertThat (coherenceHits vs |> List.length) (isEqualTo 0)

let private assertExceptionFamilyQuiet (_src: string) (vs: EnergyViolation list) =
    assertThat (coherenceHits vs |> List.length) (isEqualTo 0)

// "flagged with the stronger message" scenarios.
let private assertEntropyDump (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "unrelated types" ] vs |> List.length > 0) isTrue

let private assertUnrelatedClassesFlagged (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "unrelated groups" ] vs |> List.length > 0) isTrue

// A single scenario: a display name, fixture filename (without language dir or extension), and the
// assertion to run after parsing + analyzing. Built into an async test per language below.
type Scenario =
    { Name: string
      File: string
      Assert: string -> EnergyViolation list -> unit }

let private buildTest (languageLabel: string) (language: LanguageAdapter) (ext: string) (scenario: Scenario) =
    let fixture = language.Id + "/coherence/" + scenario.File + "." + ext

    testAsync (
        (sprintf "%s: %s" languageLabel scenario.Name),
        (fun _ ->
            toAsync (
                task {
                    let! (sourceCode, tree) = parseFixture language fixture

                    let violations = analyzeFixture sourceCode tree language fixture
                    assertValidPositions violations sourceCode

                    scenario.Assert sourceCode violations
                }
            ))
    )

let tests =
    // decision: the type-cohesive-without-naming fixture is a regression guard for a real false
    // positive — an F#-style module exposing one verb per operation over a shared domain type, no
    // naming cohesion at all, well past the generic 12-function threshold. Confirmed to misfire under
    // the naming-only heuristic before the type-cohesion signal existed (see coherence.ts's decision
    // comments). The relatedClasses/exceptionFamily fixtures guard class-grouping false positives.
    let functionLanguages =
        [ "Python", PYTHON, "py"
          "TypeScript", TYPESCRIPT, "ts"
          "F#", FSHARP, "fs"
          "Kotlin", KOTLIN, "kt"
          "C++", CPP, "cpp" ]

    let classLanguages =
        [ "Python", PYTHON, "py"
          "TypeScript", TYPESCRIPT, "ts"
          "Kotlin", KOTLIN, "kt"
          "C++", CPP, "cpp" ]

    let block1Scenarios: Scenario list =
        [ { Name = "too many large functions is flagged"
            File = "manyLargeFunctions"
            Assert = assertManyLargeFunctions }
          { Name = "import sprawl is flagged"
            File = "manyImports"
            Assert = assertManyImports }
          { Name = "many imports from one source stays quiet"
            File = "narrowImports"
            Assert = assertNarrowImportsQuiet }
          { Name = "a small module stays quiet"
            File = "clean"
            Assert = assertCleanQuiet }
          { Name = "a type-cohesive module with no naming cohesion stays quiet"
            File = "typeCohesive"
            Assert = assertTypeCohesiveQuiet }
          { Name = "a module with distinct names AND unrelated types gets the stronger entropy-dump message"
            File = "entropyDump"
            Assert = assertEntropyDump } ]

    let block2Scenarios: Scenario list =
        [ { Name = "two classes that construct/return each other stay quiet"
            File = "relatedClasses"
            Assert = assertRelatedClassesQuiet }
          { Name = "two classes with no shared inheritance, type reference, or naming pattern are flagged"
            File = "unrelatedClasses"
            Assert = assertUnrelatedClassesFlagged }
          { Name = "classes sharing a common base with no naming pattern stay quiet"
            File = "exceptionFamily"
            Assert = assertExceptionFamilyQuiet } ]

    // Build one test per (language, scenario) pair. `block1Scenarios`/`block2Scenarios` are Scenario
    // lists; the language triplets carry the extension as a one-element list so this stays uniform.
    let buildBlock (langCases: (string * LanguageAdapter * string) list) (scenarios: Scenario list) =
        langCases
        |> List.collect (fun (label, language, ext) -> scenarios |> List.map (fun s -> buildTest label language ext s))

    let block1 = buildBlock functionLanguages block1Scenarios
    let block2 = buildBlock classLanguages block2Scenarios

    testList ("Integration: file coherence (real code examples)", block1 @ block2)
