module Energy.Tests.CSharpTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Languages
open Energy.Languages.Registry
open Energy.Tests.TestUtils

let rec private nodesOfType (expected: NodeType) (node: Node) : Node list =
    let own = if nodeType node = expected then [ node ] else []
    own @ (nodeChildren node |> List.collect (nodesOfType expected))

let tests =
    testList (
        "C# language adapter and registry",
        [ test (
              "registers C# source and script suffixes case-insensitively",
              fun _ ->
                  for suffix in [ ".cs"; ".csx" ] do
                      assertThat
                          (resolveLanguageForFile ("Widget" + suffix) |> Option.map _.Id)
                          (isEqualTo (Some "csharp"))

                  assertThat (resolveLanguageForFile "Widget.CS" |> Option.map _.Id) (isEqualTo (Some "csharp"))
          )
          testAsync (
              "extracts C# declarations, control flow, imports, and constants",
              fun _ ->
                  toAsync (
                      task {
                          let! (_, tree) = parseFixture CSharp.cSharpLanguageAdapter "csharp/Adapter.cs"

                          let parameters =
                              nodesOfType (NodeType "parameter") tree
                              |> List.choose CSharp.cSharpLanguageAdapter.ExtractTypedParameter

                          assertThat
                              (parameters |> List.map (fun parameter -> parameter.Name, parameter.Type))
                              (isEqualTo [ "mode", "string"; "retries", "int"; "verbose", "bool"; "retries", "int" ])

                          let methods =
                              nodesOfType (NodeType "method_declaration") tree
                              |> List.choose CSharp.cSharpLanguageAdapter.ExtractReturnType

                          assertThat methods (isEqualTo [ "string"; "void" ])

                          let imports =
                              nodesOfType (NodeType "using_directive") tree
                              |> List.collect CSharp.cSharpLanguageAdapter.ImportInfo
                              |> List.map _.Source

                          assertThat imports (isEqualTo [ "System.Collections.Generic" ])

                          let derived = nodesOfType (NodeType "class_declaration") tree |> List.head

                          assertThat (CSharp.cSharpLanguageAdapter.GetClassName derived) (isEqualTo (Some "Derived"))

                          assertThat
                              (CSharp.cSharpLanguageAdapter.GetBaseClassNames derived)
                              (isEqualTo [ "Base"; "IWorker" ])

                          assertThat
                              (nodesOfType (NodeType "integer_literal") tree
                               |> List.exists CSharp.cSharpLanguageAdapter.IsDefaultParameterValue)
                              isTrue

                          assertThat
                              (nodesOfType (NodeType "field_declaration") tree
                               |> List.exists CSharp.cSharpLanguageAdapter.IsExplicitConstant)
                              isTrue

                          assertThat
                              (nodesOfType (NodeType "interpolated_string_expression") tree
                               |> List.isEmpty
                               |> not)
                              isTrue
                      }
                  )
          )
          testAsync (
              "runs the shared detector pipeline over C#",
              fun _ ->
                  toAsync (
                      task {
                          let! (source, tree) = parseFixture CSharp.cSharpLanguageAdapter "csharp/Adapter.cs"

                          let violations =
                              analyzeFixture source tree CSharp.cSharpLanguageAdapter "Adapter.cs"

                          assertValidPositions violations source

                          assertThat (violations |> List.exists (fun violation -> violation.Type = Magic)) isTrue

                          assertThat
                              (violations |> List.exists (fun violation -> violation.Type = OpaqueBoolean))
                              isTrue
                      }
                  )
          ) ]
    )
