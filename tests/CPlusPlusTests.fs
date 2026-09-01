module Energy.Tests.CPlusPlusTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Scan
open Energy.Core.TypeCohesion
open Energy.Languages.CPlusPlus
open Energy.Languages.Registry
open Energy.Tests.TestUtils

let rec private nodesOfType (expected: NodeType) (node: Node) : Node list =
    let own = if nodeType node = expected then [ node ] else []
    own @ (nodeChildren node |> List.collect (nodesOfType expected))

let private supportedSuffixes =
    [ ".cpp"
      ".cppm"
      ".cc"
      ".ccm"
      ".cxx"
      ".cxxm"
      ".c++"
      ".c++m"
      ".hpp"
      ".hh"
      ".hxx"
      ".h++"
      ".h"
      ".ii"
      ".ino"
      ".inl"
      ".ipp"
      ".ixx"
      ".mpp"
      ".mxx"
      ".tpp"
      ".txx"
      ".hpp.in"
      ".h.in" ]

let tests =
    testList (
        "C++ language adapter and registry",
        [ test (
              "registers every VS Code C++ suffix with longest-suffix, case-insensitive matching",
              fun _ ->
                  for suffix in supportedSuffixes do
                      let resolved = resolveLanguageForFile ("Widget" + suffix) |> Option.map _.Id
                      assertThat resolved (isEqualTo (Some "cpp"))

                  assertThat (resolveLanguageForFile "Widget.HPP.IN" |> Option.map _.Id) (isEqualTo (Some "cpp"))
          )
          test (
              "keeps compound glob suffixes exact",
              fun _ ->
                  let resolved = resolveSupportedFiles [ "tests/scan-fixtures/**/*.hpp.in" ] (cwd ())

                  // decision: fully qualified instead of an `open` — this file sits at the coherence
                  // detector's 10-import threshold, and Paths is needed at exactly this one assertion.
                  let (Energy.Core.Paths.Path file) = List.head resolved

                  assertThat resolved.Length (isEqualTo 1)
                  assertThat (file.EndsWith("widget.hpp.in")) isTrue
          )
          test (
              "does not claim C, CUDA, or Objective-C++ suffixes",
              fun _ ->
                  for suffix in [ ".c"; ".i"; ".cu"; ".cuh"; ".mm"; ".m" ] do
                      let unresolved =
                          match resolveLanguageForFile ("source" + suffix) with
                          | None -> true
                          | Some _ -> false

                      assertThat unresolved isTrue
          )
          test (
              "accepts qualified C++ names as type-cohesion signals",
              fun _ ->
                  assertThat (baseTypeName "std::vector<int>" CPP.GenericBrackets) (isEqualTo (Some "std::vector"))

                  assertThat (baseTypeName "std::string" CPP.GenericBrackets) (isEqualTo (Some "std::string"))
          )
          testAsync (
              "extracts declarators, trailing returns, includes, constants, and inheritance",
              fun _ ->
                  toAsync (
                      task {
                          let! (_, tree) = parseFixture CPP "cpp/adapter.cpp"

                          let parameters =
                              CPP.ParameterChildTypes
                              |> List.collect (fun nodeType -> nodesOfType nodeType tree)
                              |> List.sortBy nodeStartIndex
                              |> List.choose CPP.ExtractTypedParameter

                          assertThat
                              (parameters |> List.map (fun parameter -> parameter.Name, parameter.Type))
                              (isEqualTo
                                  [ "name", "std::string&"
                                    "count", "int"
                                    "pointer", "int*"
                                    "values", "std::vector<int>" ])

                          let returns =
                              nodesOfType (NodeType "function_definition") tree
                              |> List.choose CPP.ExtractReturnType

                          assertThat returns (isEqualTo [ "std::string"; "std::string" ])

                          let includes =
                              nodesOfType (NodeType "preproc_include") tree |> List.map CPP.ImportSource

                          assertThat includes (isEqualTo [ "vector"; "thing.hpp" ])

                          let structure = nodesOfType (NodeType "struct_specifier") tree |> List.head
                          assertThat (CPP.GetClassName structure) (isEqualTo (Some "Derived"))
                          assertThat (CPP.GetBaseClassNames structure) (isEqualTo [ "ns::Base"; "Interface<int>" ])

                          assertThat
                              (nodesOfType (NodeType "number_literal") tree
                               |> List.exists CPP.IsDefaultParameterValue)
                              isTrue

                          assertThat
                              (nodesOfType (NodeType "declaration") tree |> List.exists CPP.IsExplicitConstant)
                              isTrue

                          assertThat
                              (nodesOfType (NodeType "enumerator") tree |> List.exists CPP.IsExplicitConstant)
                              isTrue
                      }
                  )
          )
          testAsync (
              "recognizes loop families and alternative boolean operators",
              fun _ ->
                  toAsync (
                      task {
                          let! (_, tree) = parseFixture CPP "cpp/adapter.cpp"

                          for nodeType in
                              [ NodeType "for_statement"
                                NodeType "for_range_loop"
                                NodeType "while_statement"
                                NodeType "do_statement" ] do
                              assertThat (nodesOfType nodeType tree |> List.isEmpty |> not) isTrue

                          assertThat
                              (nodesOfType (NodeType "binary_expression") tree
                               |> List.choose CPP.GetBooleanOperator
                               |> List.isEmpty
                               |> not)
                              isTrue
                      }
                  )
          )
          testAsync (
              "ignores forward declarations and keeps class methods out of free-function sprawl",
              fun _ ->
                  toAsync (
                      task {
                          let! (source, tree) = parseFixture CPP "cpp/coherence/classBoundaries.cpp"
                          let violations = analyzeFixture source tree CPP "classBoundaries.hpp"

                          assertThat
                              (violations
                               |> List.filter (fun violation -> violation.Type = Coherence)
                               |> List.length)
                              (isEqualTo 0)
                      }
                  )
          )
          testAsync (
              "honors slash-comment suppression directives in C++ source",
              fun _ ->
                  toAsync (
                      task {
                          let! (source, tree) = parseFixture CPP "cpp/suppressions.cpp"
                          let violations = analyzeFixture source tree CPP "suppressions.cpp"

                          assertThat
                              (violations |> List.exists (fun violation -> violation.Type = PrimitiveObsession))
                              isFalse

                          assertThat (violations |> List.exists (fun violation -> violation.Type = Suppression)) isFalse
                      }
                  )
          ) ]
    )
