// # GrammarTests
//
// This suite exercises grammar caching and initialization scenarios; each case describes the positive
// result, exemption, boundary, or failure contract it protects. Tests are registered by Main.fs. Shared
// parsing and fixture assertions live in TestUtils.fs, while named case descriptions below state the
// protected outcomes.
//
// Reading path: [Source guide](../docs/source-guide.md).

module Energy.Tests.GrammarTests

open System.Collections.Generic

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Extension.Grammar

/// Run the named grammar caching and initialization scenarios below; each case describes the positive
/// result, exemption, boundary, or failure contract it protects.
let tests =
    testList (
        "Extension grammar loading",
        [
            testAsync (
                "a failed load leaves no in-flight entry and a later request retries",
                fun _ ->
                    toAsync (
                        task {
                            do! initializeParser ()

                            let context: GrammarContext =
                                {
                                    ExtensionPath = "/nonexistent-energy-state-extension"
                                    LoadedLanguages = Dictionary()
                                    InFlightLoads = Dictionary()
                                }

                            let! first = getOrLoadLanguage "python" context

                            match first with
                            | Error _ -> ()
                            | Ok _ -> failwith "Expected grammar loading to fail"

                            assertThat context.InFlightLoads.Count (isEqualTo 0)

                            let! second = getOrLoadLanguage "python" context

                            match second with
                            | Error _ -> ()
                            | Ok _ -> failwith "Expected grammar retry to fail at the same missing path"

                            assertThat context.InFlightLoads.Count (isEqualTo 0)
                        }
                    )
            )
        ]
    )
