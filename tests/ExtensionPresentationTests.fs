module Energy.Tests.ExtensionPresentationTests

open Scriptorium.Nib.Assertion
open Scriptorium.Quill
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Extension.ConfigurationValues
open Energy.Extension.DecorationModel
open Energy.Extension.DiagnosticModel

let private violation line column severity violationType message hotspots =
    { Line = line
      Column = column
      Type = violationType
      Severity = severity
      Message = message
      Hotspots = hotspots }

let private defaults =
    { Bool = fun _ _ fallback -> fallback
      Int = fun _ _ fallback -> fallback
      Float = fun _ _ fallback -> fallback
      Floats = fun _ _ fallback -> fallback
      String = fun _ _ fallback -> fallback
      Strings = fun _ _ fallback -> fallback
      GlobalBool = fun _ fallback -> fallback }

let tests =
    testList (
        "Extension presentation and configuration",
        [ test (
              "reads every configured threshold and color from an injected settings boundary",
              fun _ ->
                  let reader =
                      { defaults with
                          Int =
                              fun section key fallback ->
                                  match section, key with
                                  | "nesting", "mediumThreshold" -> 4
                                  | "matchOpportunity", "minBranches" -> 5
                                  | _ -> fallback
                          Float =
                              fun section key fallback ->
                                  match section, key with
                                  | "coherence", "singleDomainNameShare" -> 0.8
                                  | "colors", "backgroundOpacity" -> 0.25
                                  | _ -> fallback
                          Bool =
                              fun section key fallback ->
                                  match section, key with
                                  | "magicNumber", "enabled" -> false
                                  | _ -> fallback
                          GlobalBool =
                              fun key fallback ->
                                  match key with
                                  | "includeTestFiles" -> true
                                  | _ -> fallback
                          Floats =
                              fun section key fallback ->
                                  if section = "magicNumber" && key = "allowlist" then
                                      [ 3.0 ]
                                  else
                                      fallback
                          String =
                              fun section key fallback ->
                                  if section = "colors" && key = "highEnergy" then
                                      "#112233"
                                  else
                                      fallback }

                  let thresholds = readAnalyzeThresholds reader
                  let colors = readEnergyColors reader
                  let nesting = thresholds.Nesting |> Option.get
                  let coherence = thresholds.Coherence |> Option.get
                  let magicNumber = thresholds.MagicNumber |> Option.get
                  let magicString = thresholds.MagicString |> Option.get
                  let matchOpportunity = thresholds.MatchOpportunity |> Option.get

                  let emptyMagicNumberAllowlist =
                      readAnalyzeThresholds
                          { defaults with
                              Floats =
                                  fun section key fallback ->
                                      if section = "magicNumber" && key = "allowlist" then
                                          []
                                      else
                                          fallback }
                      |> _.MagicNumber
                      |> Option.get

                  let hostArrayMagicNumberAllowlist =
                      readAnalyzeThresholds
                          { defaults with
                              Floats =
                                  fun section key fallback ->
                                      if section = "magicNumber" && key = "allowlist" then
                                          floatsFromConfiguration [| 0.0; 1.0; -1.0; 2.0; 3.0 |]
                                      else
                                          fallback }
                      |> _.MagicNumber
                      |> Option.get

                  assertThat nesting.MediumThreshold (isEqualTo 4)
                  assertThat coherence.SingleDomainNameShare (isEqualTo 0.8)
                  assertThat magicNumber.Enabled isFalse
                  assertThat magicNumber.Allowlist (isEqualTo [ 0.0; 1.0; -1.0; 2.0; 3.0 ])
                  assertThat emptyMagicNumberAllowlist.Allowlist (isEqualTo [ 0.0; 1.0; -1.0; 2.0 ])
                  assertThat hostArrayMagicNumberAllowlist.Allowlist (isEqualTo [ 0.0; 1.0; -1.0; 2.0; 3.0 ])
                  assertThat magicNumber.IncludeTestFiles isTrue
                  assertThat magicString.IncludeTestFiles isTrue
                  assertThat matchOpportunity.MinBranches (isEqualTo 5)
                  assertThat colors.HighEnergy (isEqualTo "#112233")
                  assertThat colors.BackgroundOpacity (isEqualTo 0.25)
          )
          test (
              "maps violation categories to their editor ranges and rejects malformed colors",
              fun _ ->
                  let coherence = violation 2 8 High Coherence "sprawl" []
                  let complexity = violation 3 6 Medium Complexity "complex" []
                  let element = violation 4 6 Low Magic "magic" []

                  assertThat
                      (rangeFor "  class Thing:" coherence)
                      (isEqualTo
                          { StartLine = 2
                            StartColumn = 0
                            EndLine = 2
                            EndColumn = 14 })

                  assertThat
                      (rangeFor "    def complex():" complexity)
                      (isEqualTo
                          { StartLine = 3
                            StartColumn = 4
                            EndLine = 3
                            EndColumn = 18 })

                  assertThat
                      (rangeFor "  key = 123" element)
                      (isEqualTo
                          { StartLine = 4
                            StartColumn = 6
                            EndLine = 4
                            EndColumn = 11 })

                  assertThat (hexToRgba "not-a-color" 0.1 "#fb8500") (isEqualTo "rgba(251, 133, 0, 0.1)")
          )
          test (
              "normalizes complexity heat per violation and ignores out-of-range hotspots",
              fun _ ->
                  let complex =
                      violation
                          0
                          0
                          Medium
                          Complexity
                          "complex"
                          [ { Line = 1; Weight = 1 }; { Line = 2; Weight = 4 }; { Line = 9; Weight = 4 } ]

                  let ranges =
                      heatRanges 4 (fun line -> [| "zero"; "one"; "two"; "three" |].[line]) 4 [ complex ]

                  assertThat ranges.[1].Length (isEqualTo 1)
                  assertThat ranges.[1].[0].StartLine (isEqualTo 1)
                  assertThat ranges.[3].Length (isEqualTo 1)
                  assertThat ranges.[3].[0].StartLine (isEqualTo 2)
          )
          test (
              "groups Problems findings by line with leading severity, tags, and combined codes",
              fun _ ->
                  let specs =
                      diagnosticSpecs
                          [ violation 8 12 Low Magic "magic" []
                            violation 8 4 High Complexity "complex" []
                            violation 8 7 Medium Nesting "nested" [] ]

                  let spec = List.exactlyOne specs
                  assertThat spec.Severity (isEqualTo Error)
                  assertThat spec.Range.StartColumn (isEqualTo 4)
                  assertThat spec.Message (isEqualTo "complex | nested | magic")
                  assertThat spec.Code (isEqualTo "energy-complexity,energy-nesting,energy-magic")
                  assertThat spec.Tags (isEqualTo [ Deprecated; Unnecessary ])
          ) ]
    )
