module Energy.Tests.GrammarTests

open System.Collections.Generic

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Extension.Grammar

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
