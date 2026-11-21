// # ParsedAnalysisTests
//
// This suite exercises parsed-tree ownership and failure mapping scenarios; each case describes the
// positive result, exemption, boundary, or failure contract it protects. Tests are registered by Main.fs.
// Shared parsing and fixture assertions live in TestUtils.fs, while named case descriptions below state
// the protected outcomes.
//
// Reading path: [Source guide](../docs/source-guide.md).

module Energy.Tests.ParsedAnalysisTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Analyze
open Energy.Core.ParsedAnalysis
open Energy.Core.Paths
open Energy.Languages.Registry

/// Run the named parsed-tree ownership and failure mapping scenarios below; each case describes the
/// positive result, exemption, boundary, or failure contract it protects.
let tests =
    testList (
        "Parsed analysis boundary",
        [
            test (
                "parser failure is returned without findings",
                fun _ ->
                    let result =
                        analyzeParsedSource null languages.["python"] (Path "broken.py") "x = 1" defaultThresholds

                    match result with
                    | Error(ParseFailed("broken.py", _)) -> ()
                    | _ -> failwith "Expected a typed parse error"
            )
        ]
    )
