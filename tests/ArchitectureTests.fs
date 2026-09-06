module Energy.Tests.ArchitectureTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Architecture
open Energy.Core.ArchitectureModel
open Energy.Core.ArchitecturePolicy

let private policy =
    { Zones =
        [ { Name = "Core"
            Files = [ "src/Core" ]
            ImportPrefixes = [ "Energy.Core" ] }
          { Name = "Extension"
            Files = [ "src/Extension" ]
            ImportPrefixes = [ "Energy.Extension" ] } ]
      ForbiddenDependencies = [ { From = "Core"; To = "Extension" } ] }

let private import filePath source =
    { FilePath = filePath
      Line = 4
      Column = 2
      Source = source }

// decision: policy tests use raw extracted imports so they pin repository-level classification and
// enforcement independently of grammar fixtures; language adapters already cover extraction itself.
let tests =
    testList (
        "Architecture: repository boundaries",
        [ test (
              "forbidden configured edge is reported with both zones",
              fun _ ->
                  let report =
                      audit policy 2 [ import "src/Core/Analyze.fs" "Energy.Extension.Analysis" ]

                  assertThat report.Violations.Length (isEqualTo 1)
                  assertThat report.Violations.Head.FromZone (isEqualTo "Core")
                  assertThat report.Violations.Head.ToZone (isEqualTo "Extension")
          )
          test (
              "allowed and external imports remain non-violating",
              fun _ ->
                  let report =
                      audit
                          policy
                          2
                          [ import "src/Extension/Extension.fs" "Energy.Core.Analyze"
                            import "src/Core/Analyze.fs" "System" ]

                  assertThat report.Violations.Length (isEqualTo 0)
                  assertThat report.UnresolvedImports.Length (isEqualTo 1)
          )
          test (
              "policy rejects dependencies naming unknown zones",
              fun _ ->
                  let result =
                      parsePolicyJson
                          """{"zones":[{"name":"Core","files":["src/Core"],"importPrefixes":["Energy.Core"]}],"forbiddenDependencies":[{"from":"Core","to":"Missing"}]}"""

                  let failed =
                      match result with
                      | Error _ -> true
                      | Ok _ -> false

                  assertThat failed isTrue
          ) ]
    )
