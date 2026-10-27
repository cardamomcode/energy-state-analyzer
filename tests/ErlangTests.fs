module Energy.Tests.ErlangTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Detectors.Cyclomatic
open Energy.Core.Violation
open Energy.Languages
open Energy.Languages.Registry
open Energy.Tests.TestUtils

/// Collect real syntax nodes of one grammar type from a parsed fixture.
let rec private nodesOfType expected node =
    let own = if nodeType node = expected then [ node ] else []
    own @ (nodeChildren node |> List.collect (nodesOfType expected))

/// Verify the Erlang grammar, adapter, and full registered pipeline together.
let tests =
    testList (
        "Erlang language adapter and registry",
        [
            test (
                "registers source and header suffixes case-insensitively",
                fun _ ->
                    for suffix in [ ".erl"; ".hrl"; ".ERL" ] do
                        assertThat
                            (resolveLanguageForFile ("adapter" + suffix) |> Option.map _.Id)
                            (isEqualTo (Some "erlang"))
            )
            testAsync (
                "parses named and anonymous clauses with imports and branching",
                fun _ ->
                    toAsync (
                        task {
                            let! (_, tree) = parseFixture Erlang.erlangLanguageAdapter "erlang/Adapter.erl"
                            assertThat (nodeHasError tree) isFalse

                            let declarations = nodesOfType (NodeType "fun_decl") tree
                            assertThat declarations.Length (isEqualTo 7)

                            let names =
                                declarations
                                |> List.collect Erlang.erlangLanguageAdapter.GetCallableViews
                                |> List.choose _.BindingName

                            assertThat
                                names
                                (isEqualTo
                                    [
                                        "clean_values"
                                        "flagged_magic"
                                        "flagged_parameters"
                                        "flagged_boolean"
                                        "branching"
                                        "recovery"
                                        "anonymous"
                                    ])

                            let sixParameters =
                                declarations
                                |> List.collect Erlang.erlangLanguageAdapter.GetCallableViews
                                |> List.find (fun callable -> callable.BindingName = Some "flagged_parameters")

                            assertThat sixParameters.Parameters.Length (isEqualTo 6)

                            let anonymous = nodesOfType (NodeType "anonymous_fun") tree
                            assertThat anonymous.Length (isEqualTo 1)

                            assertThat
                                (anonymous
                                 |> List.collect Erlang.erlangLanguageAdapter.GetCallableViews
                                 |> List.length)
                                (isEqualTo 1)

                            let imports =
                                nodesOfType (NodeType "import_attribute") tree
                                |> List.collect Erlang.erlangLanguageAdapter.ImportInfo

                            assertThat (imports |> List.map _.Source) (isEqualTo [ "lists" ])

                            assertThat
                                (imports |> List.collect _.Bindings |> List.map _.LocalName)
                                (isEqualTo [ "reverse" ])

                            let cases = nodesOfType (NodeType "case_expr") tree

                            assertThat
                                (cases |> List.map Erlang.erlangLanguageAdapter.CyclomaticBranchCount)
                                (isEqualTo [ Some 2; Some 4; Some 2 ])
                        }
                    )
            )
            testAsync (
                "scores Erlang branches and runs all registered detectors",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture Erlang.erlangLanguageAdapter "erlang/Adapter.erl"

                            let branch =
                                nodesOfType (NodeType "fun_decl") tree
                                |> List.find (fun node ->
                                    Erlang.erlangLanguageAdapter.GetCallableViews node
                                    |> List.exists (fun callable -> callable.BindingName = Some "branching"))

                            assertThat (complexityOf Erlang.erlangLanguageAdapter branch) (isEqualTo 4)

                            let violations =
                                analyzeFixture source tree Erlang.erlangLanguageAdapter "Adapter.erl"

                            assertValidPositions violations source
                            assertThat (violations |> List.exists (fun violation -> violation.Type = Magic)) isTrue
                            assertThat (violations |> List.exists (fun violation -> violation.Type = Parameters)) isTrue

                            assertThat
                                (violations |> List.exists (fun violation -> violation.Type = OpaqueBoolean))
                                isTrue
                        }
                    )
            )
            testAsync (
                "treats export arities as syntax and include_lib as an import",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) =
                                parseFixture Erlang.erlangLanguageAdapter "erlang/Attributes.erl"

                            assertThat (nodeHasError tree) isFalse

                            let includes = nodesOfType (NodeType "pp_include_lib") tree
                            let imports = includes |> List.collect Erlang.erlangLanguageAdapter.ImportInfo

                            assertThat (imports |> List.map _.Source) (isEqualTo [ "stdlib/include/ms_transform.hrl" ])

                            let violations =
                                analyzeFixture source tree Erlang.erlangLanguageAdapter "Attributes.erl"

                            assertThat
                                (violations
                                 |> List.exists (fun violation ->
                                     violation.Type = Magic && violation.Message.Contains("Magic number: 6")))
                                isFalse
                        }
                    )
            )
            testAsync (
                "counts an unmatched case path and leaves try-of success flow unmeasured",
                fun _ ->
                    toAsync (
                        task {
                            let! (_, controlTree) =
                                parseFixture Erlang.erlangLanguageAdapter "erlang/ControlFlow.erl"

                            assertThat (nodeHasError controlTree) isFalse

                            let incompleteCase =
                                nodesOfType (NodeType "case_expr") controlTree
                                |> List.find (fun node ->
                                    nodeNamedChildren node
                                    |> List.filter (fun child -> nodeType child = NodeType "cr_clause")
                                    |> List.length
                                    |> (=) 1)

                            assertThat
                                (Erlang.erlangLanguageAdapter.CyclomaticBranchCount incompleteCase)
                                (isEqualTo (Some 2))

                            let! (_, adapterTree) =
                                parseFixture Erlang.erlangLanguageAdapter "erlang/Adapter.erl"

                            let tryOf = nodesOfType (NodeType "try_expr") adapterTree |> List.head
                            assertThat (Erlang.erlangLanguageAdapter.GetErrorHandlingRegion tryOf) (isEqualTo None)
                        }
                    )
            )
            testAsync (
                "counts Erlang if and receive clauses including timeout",
                fun _ ->
                    toAsync (
                        task {
                            let! (_, tree) = parseFixture Erlang.erlangLanguageAdapter "erlang/FlowForms.erl"
                            assertThat (nodeHasError tree) isFalse

                            let conditional = nodesOfType (NodeType "if_expr") tree |> List.head
                            let receiveForm = nodesOfType (NodeType "receive_expr") tree |> List.head

                            assertThat
                                (Erlang.erlangLanguageAdapter.CyclomaticBranchCount conditional)
                                (isEqualTo (Some 2))

                            assertThat
                                (Erlang.erlangLanguageAdapter.CyclomaticBranchCount receiveForm)
                                (isEqualTo (Some 3))
                        }
                    )
            )
        ]
    )
