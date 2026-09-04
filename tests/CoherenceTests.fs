module Energy.Tests.CoherenceTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Core.LanguageAdapter
open Energy.Core.Analyze
open Energy.Languages
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

    assertThat
        (hitsWithMessage [ "broad dependency surface"; "sibling modules" ] vs
         |> List.length > 0)
        isTrue

let private assertFSharpImportScopeSprawl (_src: string) (vs: EnergyViolation list) =
    assertThat
        (hitsWithMessage [ "Import scope sprawl"; "Energy.Languages"; "name-resolution risk" ] vs
         |> List.length > 0)
        isTrue

let private assertKotlinMemberImportFanOut (_src: string) (vs: EnergyViolation list) =
    assertThat
        (hitsWithMessage [ "Import member fan-out"; "example.services"; "local vocabulary" ] vs
         |> List.length > 0)
        isTrue

let private assertMemberImportFanOut (_src: string) (vs: EnergyViolation list) =
    assertThat
        (hitsWithMessage [ "Import member fan-out"; "local vocabulary" ] vs
         |> List.length > 0)
        isTrue

let private assertWildcardImportScopePollution (_src: string) (vs: EnergyViolation list) =
    assertThat
        (hitsWithMessage [ "Import scope pollution"; "wildcard import" ] vs
         |> List.length > 0)
        isTrue

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

// "flagged" scenarios for the god-class (large-class) sub-check of coherence.
let private assertGodClassFlagged (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "methods spanning" ] vs |> List.length > 0) isTrue

// "stays quiet" scenario: a stateless value type with a rich but cohesive combinator API must not be
// flagged as a god class, even though it has more methods than the count bar. A regression guard for
// the "module-like value type used for method chaining" case (e.g. an Option of combinators).
let private assertCohesiveValueQuiet (_src: string) (vs: EnergyViolation list) =
    assertThat (coherenceHits vs |> List.length) (isEqualTo 0)

// "flagged with the stronger message" scenarios.
let private assertEntropyDump (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "unrelated types" ] vs |> List.length > 0) isTrue

let private assertUnrelatedClassesFlagged (_src: string) (vs: EnergyViolation list) =
    assertThat (hitsWithMessage [ "unrelated groups" ] vs |> List.length > 0) isTrue

// A single scenario: a display name, a per-extension fixture filename (without the language dir), and
// the assertion to run after parsing + analyzing. The stem is stored per extension because each
// language uses its own file-name casing (snake_case for Python/C++, camelCase for TypeScript,
// PascalCase for F#/Kotlin), so one shared stem can no longer resolve across all five.
// Built into an async test per language below.
type Scenario =
    { Name: string
      Files: Map<string, string>
      Assert: string -> EnergyViolation list -> unit }

let private buildTest (languageLabel: string) (language: LanguageAdapter) (ext: string) (scenario: Scenario) =
    let fixture = language.Id + "/coherence/" + Map.find ext scenario.Files + "." + ext

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
        [ "Python", Python.pythonLanguageAdapter, "py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "ts"
          "F#", FSharp.fSharpLanguageAdapter, "fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp" ]

    let classLanguages =
        [ "Python", Python.pythonLanguageAdapter, "py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "ts"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp" ]

    let block1Scenarios: Scenario list =
        [ { Name = "too many large functions is flagged"
            Files =
              Map.ofList
                  [ "py", "many_large_functions"
                    "ts", "manyLargeFunctions"
                    "fs", "ManyLargeFunctions"
                    "kt", "ManyLargeFunctions"
                    "cpp", "many_large_functions" ]
            Assert = assertManyLargeFunctions }
          { Name = "import sprawl is flagged"
            Files =
              Map.ofList
                  [ "py", "many_imports"
                    "ts", "manyImports"
                    "fs", "ManyImports"
                    "kt", "ManyImports"
                    "cpp", "many_imports" ]
            Assert = assertManyImports }
          { Name = "many imports from one source stays quiet"
            Files =
              Map.ofList
                  [ "py", "narrow_imports"
                    "ts", "narrowImports"
                    "fs", "NarrowImports"
                    "kt", "NarrowImports"
                    "cpp", "narrow_imports" ]
            Assert = assertNarrowImportsQuiet }
          { Name = "a small module stays quiet"
            Files = Map.ofList [ "py", "clean"; "ts", "clean"; "fs", "Clean"; "kt", "Clean"; "cpp", "clean" ]
            Assert = assertCleanQuiet }
          { Name = "a type-cohesive module with no naming cohesion stays quiet"
            Files =
              Map.ofList
                  [ "py", "type_cohesive"
                    "ts", "typeCohesive"
                    "fs", "TypeCohesive"
                    "kt", "TypeCohesive"
                    "cpp", "type_cohesive" ]
            Assert = assertTypeCohesiveQuiet }
          { Name = "a module with distinct names AND unrelated types gets the stronger entropy-dump message"
            Files =
              Map.ofList
                  [ "py", "entropy_dump"
                    "ts", "entropyDump"
                    "fs", "EntropyDump"
                    "kt", "EntropyDump"
                    "cpp", "entropy_dump" ]
            Assert = assertEntropyDump } ]

    let block2Scenarios: Scenario list =
        [ { Name = "two classes that construct/return each other stay quiet"
            Files =
              Map.ofList
                  [ "py", "related_classes"
                    "ts", "relatedClasses"
                    "fs", "RelatedClasses"
                    "kt", "RelatedClasses"
                    "cpp", "related_classes" ]
            Assert = assertRelatedClassesQuiet }
          { Name = "two classes with no shared inheritance, type reference, or naming pattern are flagged"
            Files =
              Map.ofList
                  [ "py", "unrelated_classes"
                    "ts", "unrelatedClasses"
                    "fs", "UnrelatedClasses"
                    "kt", "UnrelatedClasses"
                    "cpp", "unrelated_classes" ]
            Assert = assertUnrelatedClassesFlagged }
          { Name = "classes sharing a common base with no naming pattern stay quiet"
            Files =
              Map.ofList
                  [ "py", "exception_family"
                    "ts", "exceptionFamily"
                    "fs", "ExceptionFamily"
                    "kt", "ExceptionFamily"
                    "cpp", "exception_family" ]
            Assert = assertExceptionFamilyQuiet } ]

    // decision: god-class scenarios live over the class-supporting languages only — F# has no class
    // construct, so a per-class metric has nothing to measure there (mirrors block2). The quiet fixture
    // is a regression guard for the "stateless module-like value type used for method chaining" case:
    // many methods over one domain type must stay unflagged.
    let godClassScenarios: Scenario list =
        [ { Name = "a class with many unrelated responsibilities is flagged as a god class"
            Files =
              Map.ofList
                  [ "py", "god_class"
                    "ts", "godClass"
                    "fs", "GodClass"
                    "kt", "GodClass"
                    "cpp", "god_class" ]
            Assert = assertGodClassFlagged }
          { Name = "a cohesive value type with many combinators stays quiet"
            Files =
              Map.ofList
                  [ "py", "cohesive_value_type"
                    "ts", "cohesiveValueType"
                    "fs", "CohesiveValueType"
                    "kt", "CohesiveValueType"
                    "cpp", "cohesive_value_type" ]
            Assert = assertCohesiveValueQuiet } ]

    // Build one test per (language, scenario) pair. `block1Scenarios`/`block2Scenarios` are Scenario
    // lists; the language triplets carry the extension as a one-element list so this stays uniform.
    let buildBlock (langCases: (string * LanguageAdapter * string) list) (scenarios: Scenario list) =
        langCases
        |> List.collect (fun (label, language, ext) -> scenarios |> List.map (fun s -> buildTest label language ext s))

    let block1 = buildBlock functionLanguages block1Scenarios
    let block2 = buildBlock classLanguages block2Scenarios
    let block3 = buildBlock classLanguages godClassScenarios

    let fSharpScopeSprawl =
        buildTest
            "F#"
            FSharp.fSharpLanguageAdapter
            "fs"
            { Name = "sibling opens warn about name resolution"
              Files =
                Map.ofList
                    [ "py", "sibling_imports"
                      "ts", "siblingImports"
                      "fs", "SiblingImports"
                      "kt", "SiblingImports"
                      "cpp", "sibling_imports" ]
              Assert = assertFSharpImportScopeSprawl }

    // decision: proves the configured sibling-open threshold actually flows into the detector, not
    // just through Config"s merge — raise it above the fixture"s 7 siblings and the scope-sprawl
    // finding disappears (a count-based import-sprawl remains, since the file still draws from >10
    // distinct modules), lower it below 7 and the sibling message returns.
    let siblingThresholdIsConfigurable =
        testAsync (
            "F#: configured sibling-open threshold controls the scope-sprawl finding",
            fun _ ->
                toAsync (
                    task {
                        let! (sourceCode, tree) =
                            parseFixture FSharp.fSharpLanguageAdapter "fsharp/coherence/SiblingImports.fs"

                        let input =
                            { Source = sourceCode
                              Tree = tree
                              Language = FSharp.fSharpLanguageAdapter
                              FileName = "SiblingImports.fs" }

                        let withThresholds (siblingOpen: int) (importBreadth: int) =
                            { defaultThresholds with
                                Coherence =
                                    { defaultThresholds.Coherence with
                                        SiblingOpenThreshold = siblingOpen
                                        ImportBreadthThreshold = importBreadth } }

                        let fired = analyzeWith (withThresholds 5 10) input |> _.Violations
                        let relaxed = analyzeWith (withThresholds 8 10) input |> _.Violations
                        // raising both thresholds above the fixture's 7 siblings and 12 distinct modules suppresses every import signal.
                        let quiet = analyzeWith (withThresholds 8 13) input |> _.Violations

                        assertThat (hitsWithMessage [ "Import scope sprawl" ] fired |> List.length > 0) isTrue
                        // raising the sibling threshold above the fixture's 7 siblings suppresses the sibling message; a count-based import-sprawl remains because the file still spans >10 modules.
                        assertThat (hitsWithMessage [ "Import scope sprawl" ] relaxed |> List.length) (isEqualTo 0)
                        assertThat (hitsWithMessage [ "Import sprawl" ] relaxed |> List.length > 0) isTrue
                        // with both thresholds raised past the fixture's 7 siblings and 12 modules, no import signal fires at all.
                        assertThat (coherenceHits quiet |> List.length) (isEqualTo 0)
                    }
                )
        )

    let functionCountThresholdIsConfigurable =
        testAsync (
            "F#: configured function-count threshold controls the sprawl finding",
            fun _ ->
                toAsync (
                    task {
                        let! (sourceCode, tree) =
                            parseFixture FSharp.fSharpLanguageAdapter "fsharp/coherence/EntropyDump.fs"

                        let input =
                            { Source = sourceCode
                              Tree = tree
                              Language = FSharp.fSharpLanguageAdapter
                              FileName = "EntropyDump.fs" }

                        // decision: raise the generic function-count bar above the fixture's 13 functions and
                        // the sprawl finding disappears; leave it at the default of 12 and it fires — proving
                        // the configured value flows into the detector, not just through Config's merge.
                        let withGeneric (genericFunctionCount: int) =
                            { defaultThresholds with
                                Coherence =
                                    { defaultThresholds.Coherence with
                                        GenericFunctionCount = genericFunctionCount } }

                        let fired = analyzeWith (withGeneric 12) input |> _.Violations
                        let relaxed = analyzeWith (withGeneric 14) input |> _.Violations

                        assertThat (hitsWithMessage [ "unrelated types" ] fired |> List.length > 0) isTrue
                        assertThat (coherenceHits relaxed |> List.length) (isEqualTo 0)
                    }
                )
        )

    let methodCountThresholdIsConfigurable =
        testAsync (
            "Python: configured god-class method-count bar controls the finding",
            fun _ ->
                toAsync (
                    task {
                        let! (sourceCode, tree) =
                            parseFixture Python.pythonLanguageAdapter "python/coherence/god_class.py"

                        let input =
                            { Source = sourceCode
                              Tree = tree
                              Language = Python.pythonLanguageAdapter
                              FileName = "god_class.py" }

                        // decision: the fixture has 17 methods, so it fires at the default medium bar of 15 but clears
                        // that bar at 18. Assert on the god-class message specifically — this file also trips an
                        // unrelated "classes split into groups" coherence hit that persists regardless of the bar,
                        // so a total coherence count would not isolate what the configured value controls.
                        let withMedium (methodCountMedium: int) =
                            { defaultThresholds with
                                Coherence =
                                    { defaultThresholds.Coherence with
                                        MethodCountMedium = methodCountMedium } }

                        let fired = analyzeWith (withMedium 15) input |> _.Violations
                        let relaxed = analyzeWith (withMedium 18) input |> _.Violations

                        assertThat (hitsWithMessage [ "methods spanning" ] fired |> List.length > 0) isTrue
                        assertThat (hitsWithMessage [ "methods spanning" ] relaxed |> List.length) (isEqualTo 0)
                    }
                )
        )

    let kotlinMemberFanOut =
        buildTest
            "Kotlin"
            Kotlin.kotlinLanguageAdapter
            "kt"
            { Name = "many imported members from one package are flagged"
              Files =
                Map.ofList
                    [ "py", "member_fan_out"
                      "ts", "memberFanOut"
                      "fs", "MemberFanOut"
                      "kt", "MemberFanOut"
                      "cpp", "member_fan_out" ]
              Assert = assertKotlinMemberImportFanOut }

    let memberFanOuts =
        [ "Python", Python.pythonLanguageAdapter, "py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "ts" ]
        |> List.map (fun (label, language, extension) ->
            buildTest
                label
                language
                extension
                { Name = "many imported members from one module are flagged"
                  Files =
                    Map.ofList
                        [ "py", "member_fan_out"
                          "ts", "memberFanOut"
                          "fs", "MemberFanOut"
                          "kt", "MemberFanOut"
                          "cpp", "member_fan_out" ]
                  Assert = assertMemberImportFanOut })

    let pythonWildcardImport =
        buildTest
            "Python"
            Python.pythonLanguageAdapter
            "py"
            { Name = "wildcard imports warn about scope pollution"
              Files =
                Map.ofList
                    [ "py", "wildcard_import"
                      "ts", "wildcardImport"
                      "fs", "WildcardImport"
                      "kt", "WildcardImport"
                      "cpp", "wildcard_import" ]
              Assert = assertWildcardImportScopePollution }

    testList (
        "Integration: file coherence (real code examples)",
        block1
        @ block2
        @ block3
        @ [ fSharpScopeSprawl
            siblingThresholdIsConfigurable
            functionCountThresholdIsConfigurable
            methodCountThresholdIsConfigurable
            kotlinMemberFanOut
            pythonWildcardImport ]
        @ memberFanOuts
    )
