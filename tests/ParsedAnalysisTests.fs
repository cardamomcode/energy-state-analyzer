module Energy.Tests.ParsedAnalysisTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.Analyze
open Energy.Core.ParsedAnalysis
open Energy.Core.Paths
open Energy.Languages.Registry

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
