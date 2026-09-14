module Energy.Tests.RecoveryRuleTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Suppressions
open Energy.Languages.Python
open Energy.Tests.TestUtils

/// Analyze the recovery fixture through the complete registered pipeline.
let private withFixture check =
    toAsync (
        task {
            let! source, tree = parseFixture pythonLanguageAdapter "python/recovery_rules.py"
            assertThat (nodeHasError tree) isFalse

            let run options =
                analyzeWith
                    options
                    { Source = source
                      Tree = tree
                      Language = pythonLanguageAdapter
                      FileName = "recovery_rules.py" }
                |> _.Violations

            check source run
        }
    )

/// Find only the requested rule's findings inside a named fixture scenario.
let private hits source findings name kind =
    violationsIn findings (findFunctionRange source (FunctionName name))
    |> List.filter (fun finding -> finding.Type = kind)

/// Exercise independent rules, configuration, anchors, and nested ownership.
let tests =
    testList (
        "Recovery rule contracts",
        [ testAsync (
              "F# task scaffolding does not inflate the denominator or share",
              fun _ ->
                  toAsync (
                      task {
                          let language = Energy.Languages.FSharp.fSharpLanguageAdapter
                          let! source, tree = parseFixture language "fsharp/RecoveryRules.fs"
                          assertThat (nodeHasError tree) isFalse
                          let findings = analyzeFixture source tree language "RecoveryRules.fs"
                          let boundary = hits source findings "taskBoundary" ErrorShadowing
                          assertThat boundary.Length (isEqualTo 1)
                          assertThat (boundary.Head.Message.Contains("8 logical items (80%) of 10")) isTrue
                          assertThat (hits source findings "multilineBinding" RecoveryDominance).Length (isEqualTo 1)
                      }
                  )
          )
          testAsync (
              "independent diagnostics and host switches",
              fun _ ->
                  withFixture (fun source run ->
                      let options =
                          { defaultAnalyzeOptions with
                              ErrorShadowing =
                                  { defaultErrorShadowingThresholds with
                                      ProtectedScope =
                                          { Threshold = 0.0
                                            HighThreshold = 0.7
                                            MinItems = 1 }
                                      RecoveryBlock = { MaxLines = 4 } } }

                      let findings = run options
                      let broad = hits source findings "twoLineProtected" ErrorShadowing
                      let dominance = hits source findings "twoLineProtected" RecoveryDominance
                      let oversized = hits source findings "twoLineProtected" OversizedRecoveryBlock
                      assertThat broad.Length (isEqualTo 1)
                      assertThat dominance.Length (isEqualTo 1)
                      assertThat oversized.Length (isEqualTo 1)
                      assertThat broad.Head.Line (isEqualTo dominance.Head.Line)
                      assertThat (source.Split('\n').[broad.Head.Line].Trim()) (isEqualTo "try:")

                      assertThat
                          (source
                              .Split('\n')
                              .[oversized.Head.Line].Trim()
                              .StartsWith("except", System.StringComparison.Ordinal))
                          isTrue

                      for disabled in
                          [ { options.ErrorShadowing with
                                RecoveryDominanceEnabled = false }
                            { options.ErrorShadowing with
                                OversizedRecoveryBlockEnabled = false } ] do
                          let result =
                              run
                                  { options with
                                      ErrorShadowing = disabled }

                          assertThat (hits source result "twoLineProtected" ErrorShadowing).Length (isEqualTo 1)

                          assertThat
                              (hits source result "twoLineProtected" RecoveryDominance).Length
                              (isEqualTo (if disabled.RecoveryDominanceEnabled then 1 else 0))

                          assertThat
                              (hits source result "twoLineProtected" OversizedRecoveryBlock).Length
                              (isEqualTo (if disabled.OversizedRecoveryBlockEnabled then 1 else 0))

                      let off =
                          run
                              { options with
                                  ErrorShadowing =
                                      { options.ErrorShadowing with
                                          Enabled = false } }

                      for kind in [ ErrorShadowing; RecoveryDominance; OversizedRecoveryBlock ] do
                          assertThat (hits source off "twoLineProtected" kind).Length (isEqualTo 0))
          )
          testAsync (
              "nested boundaries and functions emit once per owned clause",
              fun _ ->
                  withFixture (fun source run ->
                      let findings = run defaultAnalyzeOptions

                      for name, count in
                          [ "nestedBoundaries", 2
                            "nestedFunction", 1
                            "multipleOversized", 3
                            "lowRecoveryShare", 1
                            "mixedCodeComments", 1 ] do
                          assertThat (hits source findings name OversizedRecoveryBlock).Length (isEqualTo count)

                      assertThat (hits source findings "compactProtected" RecoveryDominance).Length (isEqualTo 0)

                      let before =
                          hits source findings "workBefore" RecoveryDominance
                          |> List.map (fun v -> v.Message, v.Severity)

                      let after =
                          hits source findings "workAfter" RecoveryDominance
                          |> List.map (fun v -> v.Message, v.Severity)

                      assertThat before (isEqualTo after))
          )
          testAsync (
              "size and share thresholds include their exact boundaries",
              fun _ ->
                  withFixture (fun source run ->
                      let defaults = run defaultAnalyzeOptions
                      assertThat (hits source defaults "shareHalf" RecoveryDominance).Head.Severity (isEqualTo Medium)
                      assertThat (hits source defaults "shareHigh" RecoveryDominance).Head.Severity (isEqualTo High)
                      assertThat (hits source defaults "belowMinimum" RecoveryDominance).Length (isEqualTo 0)
                      assertThat (hits source defaults "broadHalf" ErrorShadowing).Head.Severity (isEqualTo Medium)
                      assertThat (hits source defaults "atLimit" OversizedRecoveryBlock).Length (isEqualTo 0)
                      assertThat (hits source defaults "overLimit" OversizedRecoveryBlock).Length (isEqualTo 1)
                      let thresholds = defaultErrorShadowingThresholds

                      let custom =
                          run
                              { defaultAnalyzeOptions with
                                  ErrorShadowing =
                                      { thresholds with
                                          RecoveryBlock = { MaxLines = 21 }
                                          Recovery =
                                              { thresholds.Recovery with
                                                  MinItems = 6 } } }

                      assertThat (hits source custom "overLimit" OversizedRecoveryBlock).Length (isEqualTo 0)
                      assertThat (hits source custom "twoLineProtected" RecoveryDominance).Length (isEqualTo 0)

                      let half =
                          run
                              { defaultAnalyzeOptions with
                                  ErrorShadowing =
                                      { thresholds with
                                          Recovery =
                                              { thresholds.Recovery with
                                                  Threshold = 5.0 / 8.0
                                                  HighThreshold = 5.0 / 8.0 } } }

                      assertThat (hits source half "workBefore" RecoveryDominance).Head.Severity (isEqualTo High))
          )
          testAsync (
              "rule-specific suppressions retain other rules",
              fun _ ->
                  withFixture (fun source run ->
                      let findings =
                          run
                              { defaultAnalyzeOptions with
                                  ErrorShadowing =
                                      { defaultErrorShadowingThresholds with
                                          ProtectedScope =
                                              { Threshold = 0.0
                                                HighThreshold = 0.7
                                                MinItems = 1 }
                                          RecoveryBlock = { MaxLines = 4 } } }

                      let scoped =
                          violationsIn findings (findFunctionRange source (FunctionName "twoLineProtected"))
                          |> List.filter (fun v ->
                              List.contains v.Type [ ErrorShadowing; RecoveryDominance; OversizedRecoveryBlock ])

                      for kind in [ ErrorShadowing; RecoveryDominance; OversizedRecoveryBlock ] do
                          let directive = "# esa-ignore-file: " + violationTypeName kind + "\n"
                          let result = applySuppressions scoped (directive + source)
                          assertThat result.SuppressionNotes.Length (isEqualTo 0)
                          assertThat result.Violations.Length (isEqualTo 2)
                          assertThat (result.Violations |> List.exists (fun v -> v.Type = kind)) isFalse)
          ) ]
    )
