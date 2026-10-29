module Energy.Tests.RustTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Detectors.Cyclomatic
open Energy.Core.Violation
open Energy.Languages
open Energy.Languages.Registry
open Energy.Tests.TestUtils

/// Find syntax nodes of one type in a parsed Rust fixture.
let rec private nodesOfType expected node =
    let own = if nodeType node = expected then [ node ] else []
    own @ (nodeChildren node |> List.collect (nodesOfType expected))

/// Verify the grammar, registry, and full detector pipeline against Rust source.
let tests =
    testList (
        "Rust language adapter and registry",
        [
            test (
                "registers Rust filenames case-insensitively",
                fun _ ->
                    for suffix in [ ".rs"; ".RS" ] do
                        assertThat
                            (resolveLanguageForFile ("adapter" + suffix) |> Option.map _.Id)
                            (isEqualTo (Some "rust"))
            )
            testAsync (
                "parses functions, closures, imports, and exhaustive match arms",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture Rust.rustLanguageAdapter "rust/Adapter.rs"
                            assertThat (nodeHasError tree) isFalse

                            let functions = nodesOfType (NodeType "function_item") tree
                            let closures = nodesOfType (NodeType "closure_expression") tree
                            assertThat functions.Length (isEqualTo 18)
                            assertThat closures.Length (isEqualTo 2)

                            let untypedClosure =
                                closures |> List.find (fun closure -> (nodeText closure).Contains("|a, b, c"))

                            let untypedView =
                                Rust.rustLanguageAdapter.GetCallableViews untypedClosure |> List.head

                            assertThat untypedView.Parameters.Length (isEqualTo 6)

                            let sixParameters =
                                functions
                                |> List.collect Rust.rustLanguageAdapter.GetCallableViews
                                |> List.find (fun callable -> callable.BindingName = Some "flagged_parameters")

                            assertThat sixParameters.Parameters.Length (isEqualTo 6)

                            let method =
                                functions
                                |> List.find (fun fn ->
                                    fn
                                    |> Rust.rustLanguageAdapter.GetCallableViews
                                    |> List.exists (fun view -> view.BindingName = Some "method"))

                            let methodView = Rust.rustLanguageAdapter.GetCallableViews method |> List.head
                            assertThat methodView.Parameters.Length (isEqualTo 1)
                            assertThat (Rust.rustLanguageAdapter.IsStaticMethod method) isFalse

                            let imports =
                                nodesOfType (NodeType "use_declaration") tree
                                |> List.collect Rust.rustLanguageAdapter.ImportInfo

                            assertThat
                                (imports |> List.map _.Source)
                                (isEqualTo [ "std::collections"; "crate::values" ])

                            assertThat
                                (imports |> List.collect _.Bindings |> List.map _.LocalName)
                                (isEqualTo [ "HashMap"; "HashSet"; "local_values" ])

                            let matches = nodesOfType (NodeType "match_expression") tree

                            assertThat
                                (matches |> List.map Rust.rustLanguageAdapter.CyclomaticBranchCount)
                                (isEqualTo [ Some 3; Some 4 ])

                            let violations = analyzeFixture source tree Rust.rustLanguageAdapter "Adapter.rs"
                            assertValidPositions violations source
                            assertThat (violations |> List.exists (fun v -> v.Type = Magic)) isTrue
                            assertThat (violations |> List.exists (fun v -> v.Type = Parameters)) isTrue
                            assertThat (violations |> List.exists (fun v -> v.Type = OpaqueBoolean)) isTrue

                            let branch =
                                functions
                                |> List.find (fun fn ->
                                    fn
                                    |> Rust.rustLanguageAdapter.GetCallableViews
                                    |> List.exists (fun view -> view.BindingName = Some "branching"))

                            assertThat (complexityOf Rust.rustLanguageAdapter branch) (isEqualTo 7)
                        }
                    )
            )
            testAsync (
                "exempts named constants and type-level array sizes but flags runtime literals",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture Rust.rustLanguageAdapter "rust/Constants.rs"
                            assertThat (nodeHasError tree) isFalse

                            let magic =
                                analyzeFixture source tree Rust.rustLanguageAdapter "Constants.rs"
                                |> List.filter (fun violation -> violation.Type = Magic)

                            assertThat magic.Length (isEqualTo 4)

                            assertThat
                                (magic
                                 |> List.map _.Message
                                 |> List.exists (fun message -> message.Contains("37_i32")))
                                isTrue
                        }
                    )
            )
            testAsync (
                "groups separately declared members under their shared source",
                fun _ ->
                    toAsync (
                        task {
                            let! source, tree = parseFixture Rust.rustLanguageAdapter "rust/ExplicitImports.rs"
                            assertThat (nodeHasError tree) isFalse

                            let imports =
                                nodesOfType (NodeType "use_declaration") tree
                                |> List.collect Rust.rustLanguageAdapter.ImportInfo

                            assertThat (imports |> List.map _.Source) (isEqualTo (List.replicate 10 "example::api"))

                            assertThat
                                (imports |> List.collect _.Bindings |> List.map _.ImportedName)
                                (isEqualTo [ "T0"; "T1"; "T2"; "T3"; "T4"; "T5"; "T6"; "T7"; "T8"; "T9" ])

                            let coherence =
                                analyzeFixture source tree Rust.rustLanguageAdapter "ExplicitImports.rs"
                                |> List.filter (fun violation -> violation.Type = Coherence)

                            assertThat coherence.Length (isEqualTo 1)
                            assertThat (coherence.Head.Message.Contains("10 declarations from example::api")) isTrue
                        }
                    )
            )
            testAsync (
                "parses Result and Option validation examples without grammar errors",
                fun _ ->
                    toAsync (
                        task {
                            let! source, tree = parseFixture Rust.rustLanguageAdapter "rust/ParseDontValidate.rs"
                            assertThat (nodeHasError tree) isFalse

                            let findings =
                                analyzeFixture source tree Rust.rustLanguageAdapter "ParseDontValidate.rs"
                                |> List.filter (fun violation -> violation.Type = ParseDontValidate)

                            assertThat findings.Length (isEqualTo 5)
                        }
                    )
            )
        ]
    )
