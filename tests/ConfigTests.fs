module Energy.Tests.ConfigTests

open System
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Fable.Core

open Energy.Core.Config
open Energy.Core.Paths

// decision: these pin the single source of truth in Config.fs. Every default threshold, magic
// option, and color hex is declared once here — the tests assert that one definition rather than
// re-deriving each detector's numbers from scattered module lets (the old duplicated design).

// node:fs writeFileSync and node:os tmpdir for temp .esaconfig.json fixtures (mirrors TestUtils's
// readFileSync idiom), so merge/allowlist tests exercise exactly what both hosts resolve through the
// public path. Import (not Emit) is used because the test bundle is ESM, where `require` is absent.
[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: Path) (contents: string) : unit = nativeOnly

[<Import("tmpdir", "node:os")>]
let private osTmpDir () : string = nativeOnly

// Write a temp .esaconfig.json and load it through the public parse+merge path, so every assertion
// covers defaults overlaid by the project file — the same resolution both hosts perform.
let private loadTempConfig (json: string) : AnalyzeOptions =
    let path =
        Path(osTmpDir () + "/esa-config-test-" + Guid.NewGuid().ToString("N") + ".json")

    writeFileSync path json
    loadAnalyzeOptionsFromConfigPath path

// decision: pin each default so a later edit to Config.fs cannot silently drift one detector's number.
let defaultsTests =
    [ testAsync (
          "nesting thresholds default 3/5",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultNestingThresholds.MediumThreshold (isEqualTo 3)
                      assertThat defaultNestingThresholds.HighThreshold (isEqualTo 5)
                  }
              )
      )
      testAsync (
          "cyclomatic thresholds default 10/15",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultCyclomaticThresholds.MediumThreshold (isEqualTo 10)
                      assertThat defaultCyclomaticThresholds.HighThreshold (isEqualTo 15)
                  }
              )
      )
      testAsync (
          "cognitive thresholds default 15/25",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultCognitiveThresholds.MediumThreshold (isEqualTo 15)
                      assertThat defaultCognitiveThresholds.HighThreshold (isEqualTo 25)
                  }
              )
      )
      testAsync (
          "coherence defaults",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultCoherenceThresholds.LargeFunctionLines (isEqualTo 20)
                      assertThat defaultCoherenceThresholds.MaxLargeFunctions (isEqualTo 5)
                      assertThat defaultCoherenceThresholds.SingleDomainNameShare (isEqualTo 0.7)
                      assertThat defaultCoherenceThresholds.MaxTypeDiversityRatio (isEqualTo 0.4)
                      assertThat defaultCoherenceThresholds.MinTypedCoverage (isEqualTo 0.5)
                      assertThat defaultCoherenceThresholds.SiblingOpenThreshold (isEqualTo 7)
                      assertThat defaultCoherenceThresholds.ImportBreadthThreshold (isEqualTo 10)
                      assertThat defaultCoherenceThresholds.HighImportBreadthThreshold (isEqualTo 15)
                      assertThat defaultCoherenceThresholds.MemberImportFanOutThreshold (isEqualTo 10)
                      assertThat defaultCoherenceThresholds.UtilsFileFunctionCount (isEqualTo 8)
                      assertThat defaultCoherenceThresholds.GenericFunctionCount (isEqualTo 12)
                      assertThat defaultCoherenceThresholds.HighFunctionCount (isEqualTo 15)
                      assertThat defaultCoherenceThresholds.MethodCountMedium (isEqualTo 15)
                      assertThat defaultCoherenceThresholds.MethodCountHigh (isEqualTo 25)
                      assertThat defaultCoherenceThresholds.LargeFunctionSeverityMultiplier (isEqualTo 1.5)
                  }
              )
      )
      testAsync (
          "match opportunity defaults",
          fun _ -> toAsync (task { assertThat defaultMatchOpportunityThresholds.MinBranches (isEqualTo 3) })
      )
      testAsync (
          "parameter count thresholds default 5/8",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultParameterCountThresholds.MediumThreshold (isEqualTo 5)
                      assertThat defaultParameterCountThresholds.HighThreshold (isEqualTo 8)
                  }
              )
      )
      testAsync (
          "magic number options default allowlist and flags",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultMagicNumberOptions.Enabled (isTrue)
                      assertThat defaultMagicNumberOptions.IncludeTestFiles (isFalse)
                      assertThat (defaultMagicNumberOptions.Allowlist |> List.contains 0.0) (isTrue)
                      assertThat (defaultMagicNumberOptions.Allowlist |> List.contains -1.0) (isTrue)
                      assertThat (defaultMagicNumberOptions.Allowlist |> List.contains 2.0) (isTrue)
                  }
              )
      )
      testAsync (
          "magic string options default allowlist and flags",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultMagicStringOptions.Enabled (isTrue)
                      assertThat defaultMagicStringOptions.MinDuplicates (isEqualTo 2)
                      assertThat defaultMagicStringOptions.IncludeTestFiles (isFalse)
                      assertThat (defaultMagicStringOptions.Allowlist |> List.contains "") (isTrue)
                      assertThat (defaultMagicStringOptions.Allowlist |> List.contains "utf-8") (isTrue)
                      assertThat (defaultMagicStringOptions.Allowlist |> List.contains "__main__") (isTrue)
                  }
              )
      )
      // decision: every detector is enabled by default, so the CLI and a fresh workspace run the full
      // suite; disabling is an explicit editor choice, never the silent baseline.
      testAsync (
          "every detector defaults to enabled",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultNestingThresholds.Enabled (isTrue)
                      assertThat defaultCyclomaticThresholds.Enabled (isTrue)
                      assertThat defaultCognitiveThresholds.Enabled (isTrue)
                      assertThat defaultCoherenceThresholds.Enabled (isTrue)
                      assertThat defaultMatchOpportunityThresholds.Enabled (isTrue)
                      assertThat defaultParameterCountThresholds.Enabled (isTrue)
                      assertThat defaultPrimitiveObsessionThresholds.Enabled (isTrue)
                      assertThat defaultOpaqueBooleanThresholds.Enabled (isTrue)
                      assertThat defaultLogicalControlFlowThresholds.Enabled (isTrue)
                      assertThat defaultInversionThresholds.Enabled (isTrue)
                  }
              )
      ) ]

// decision: the default amber is declared once here, so the editor and CI can never disagree on it.
let colorsTests =
    [ testAsync (
          "energy color hexes default to one source",
          fun _ ->
              toAsync (
                  task {
                      assertThat defaultEnergyColors.HighEnergy (isEqualTo "#fb8500")
                      assertThat defaultEnergyColors.MediumEnergy (isEqualTo "#ffb703")
                      assertThat defaultEnergyColors.LowEnergy (isEqualTo "#99dd99")
                      assertThat defaultEnergyColors.BackgroundOpacity (isEqualTo 0.1)
                  }
              )
      ) ]

// decision: a project file overrides the matching field but leaves every other field at its default,
// so one .esaconfig.json can retune nesting while keeping the built-in cyclomatic/cognitive numbers.
let mergePrecedenceTests =
    [ testAsync (
          "a provided threshold overrides its default and leaves siblings untouched",
          fun _ ->
              toAsync (
                  task {
                      let merged = loadTempConfig """{"nesting": {"mediumThreshold": 50}}"""
                      let mediumThreshold = merged.Nesting.MediumThreshold
                      let highThreshold = merged.Nesting.HighThreshold
                      let cyclomaticMedium = merged.Cyclomatic.MediumThreshold

                      assertThat mediumThreshold (isEqualTo 50)
                      // the sibling high threshold was not in the file, so it stays the built-in default of 5.
                      assertThat highThreshold (isEqualTo 5)
                      // an unrelated section keeps its own default rather than picking up the nesting override.
                      assertThat cyclomaticMedium (isEqualTo 10)
                  }
              )
      )

      testAsync (
          "a provided coherence ratio overrides only that field",
          fun _ ->
              toAsync (
                  task {
                      let merged = loadTempConfig """{"coherence": {"maxLargeFunctions": 8}}"""
                      let maxLargeFunctions = merged.Coherence.MaxLargeFunctions
                      let largeFunctionLines = merged.Coherence.LargeFunctionLines

                      assertThat maxLargeFunctions (isEqualTo 8)
                      assertThat largeFunctionLines (isEqualTo 20)
                  }
              )
      )

      testAsync (
          "a provided sibling-open threshold overrides only that field",
          fun _ ->
              toAsync (
                  task {
                      let merged = loadTempConfig """{"coherence": {"siblingOpenThreshold": 10}}"""
                      let siblingOpenThreshold = merged.Coherence.SiblingOpenThreshold
                      let maxLargeFunctions = merged.Coherence.MaxLargeFunctions

                      assertThat siblingOpenThreshold (isEqualTo 10)
                      // an unrelated coherence field keeps its built-in default of 5.
                      assertThat maxLargeFunctions (isEqualTo 5)
                  }
              )
      )

      testAsync (
          "a provided import-breadth and member-fan-out threshold override only their fields",
          fun _ ->
              toAsync (
                  task {
                      let merged =
                          loadTempConfig
                              """{"coherence": {"importBreadthThreshold": 20, "memberImportFanOutThreshold": 3}}"""

                      let importBreadthThreshold = merged.Coherence.ImportBreadthThreshold
                      let memberImportFanOutThreshold = merged.Coherence.MemberImportFanOutThreshold
                      let siblingOpenThreshold = merged.Coherence.SiblingOpenThreshold

                      assertThat importBreadthThreshold (isEqualTo 20)
                      assertThat memberImportFanOutThreshold (isEqualTo 3)
                      // unrelated import-coherence fields keep their built-in defaults.
                      assertThat siblingOpenThreshold (isEqualTo 7)
                  }
              )
      )
      testAsync (
          "a provided function-count sprawl threshold overrides only its field",
          fun _ ->
              toAsync (
                  task {
                      let merged =
                          loadTempConfig
                              """{"coherence": {"genericFunctionCount": 20, "highFunctionCount": 30, "utilsFileFunctionCount": 12, "largeFunctionSeverityMultiplier": 2.0}}"""

                      let genericFunctionCount = merged.Coherence.GenericFunctionCount
                      let highFunctionCount = merged.Coherence.HighFunctionCount
                      let utilsFileFunctionCount = merged.Coherence.UtilsFileFunctionCount

                      let largeFunctionSeverityMultiplier =
                          merged.Coherence.LargeFunctionSeverityMultiplier

                      let maxLargeFunctions = merged.Coherence.MaxLargeFunctions
                      let siblingOpenThreshold = merged.Coherence.SiblingOpenThreshold

                      // the four provided function-count sprawl fields take their provided values.
                      assertThat genericFunctionCount (isEqualTo 20)
                      assertThat highFunctionCount (isEqualTo 30)
                      assertThat utilsFileFunctionCount (isEqualTo 12)
                      assertThat largeFunctionSeverityMultiplier (isEqualTo 2.0)
                      // unrelated coherence fields keep their built-in defaults.
                      assertThat maxLargeFunctions (isEqualTo 5)
                      assertThat siblingOpenThreshold (isEqualTo 7)
                  }
              )
      )

      testAsync (
          "a provided god-class method-count threshold overrides only that field",
          fun _ ->
              toAsync (
                  task {
                      let merged =
                          loadTempConfig
                              """{"coherence": {"godClassMethodCountMedium": 20, "godClassMethodCountHigh": 30}}"""

                      let methodCountMedium = merged.Coherence.MethodCountMedium
                      let methodCountHigh = merged.Coherence.MethodCountHigh
                      let highFunctionCount = merged.Coherence.HighFunctionCount
                      let maxLargeFunctions = merged.Coherence.MaxLargeFunctions

                      // the two provided god-class bars take their provided values.
                      assertThat methodCountMedium (isEqualTo 20)
                      assertThat methodCountHigh (isEqualTo 30)
                      // unrelated coherence fields keep their built-in defaults.
                      assertThat highFunctionCount (isEqualTo 15)
                      assertThat maxLargeFunctions (isEqualTo 5)
                  }
              )
      )
      testAsync (
          "a provided parameter count threshold overrides only that field",
          fun _ ->
              toAsync (
                  task {
                      let merged = loadTempConfig """{"parameterCount": {"mediumThreshold": 10}}"""
                      let mediumThreshold = merged.ParameterCount.MediumThreshold
                      let highThreshold = merged.ParameterCount.HighThreshold

                      assertThat mediumThreshold (isEqualTo 10)
                      // the sibling high threshold was not in the file, so it stays the built-in default of 8.
                      assertThat highThreshold (isEqualTo 8)
                  }
              )
      ) ]

// decision: a provided allowlist is UNIONED with the structural/sentinel literals, never replacing
// them — so 0/1/-1/2 and "" / "utf-8" / "__main__" stay exempt no matter what a project sets.
let allowlistUnioningTests =
    [ testAsync (
          "magic number keeps structural literals when the file omits an allowlist",
          fun _ ->
              toAsync (
                  task {
                      let merged = loadTempConfig "{}"
                      let magicNumberAllowlist = merged.MagicNumber.Allowlist

                      assertThat (magicNumberAllowlist |> List.contains 0.0) (isTrue)
                      assertThat (magicNumberAllowlist |> List.contains 1.0) (isTrue)
                      assertThat (magicNumberAllowlist |> List.contains -1.0) (isTrue)
                      assertThat (magicNumberAllowlist |> List.contains 2.0) (isTrue)
                  }
              )
      )

      testAsync (
          "magic string unions a provided allowlist with the structural literals",
          fun _ ->
              toAsync (
                  task {
                      let merged = loadTempConfig """{"magicString": {"allowlist": ["custom"]}}"""
                      let magicStringAllowlist = merged.MagicString.Allowlist

                      assertThat (magicStringAllowlist |> List.contains "custom") (isTrue)
                      assertThat (magicStringAllowlist |> List.contains "") (isTrue)
                      assertThat (magicStringAllowlist |> List.contains "utf-8") (isTrue)
                      assertThat (magicStringAllowlist |> List.contains "__main__") (isTrue)
                  }
              )
      ) ]

let tests =
    testList (
        "Config: single source of truth",
        defaultsTests @ colorsTests @ mergePrecedenceTests @ allowlistUnioningTests
    )
