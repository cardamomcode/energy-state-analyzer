module Energy.Tests.TsxTests

open System.Collections.Generic
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.FsPath
open Energy.Core.Paths
open Energy.Core.Scan
open Energy.Core.TreeSitter
open Energy.Core.Detectors.Cyclomatic
open Energy.Core.Violation
open Energy.Extension.Grammar
open Energy.Languages
open Energy.Languages.Registry
open Energy.Tests.TestUtils

/// Collect callable views from every syntax node, including JSX expression children.
let rec private callables node =
    TypeScript.tsxLanguageAdapter.GetCallableViews node
    @ (nodeNamedChildren node |> List.collect callables)

/// Cover TSX discovery, JSX parsing, shared detectors, and editor grammar isolation.
let tests =
    testList (
        "TSX support",
        [
            test (
                "routes TSX case-insensitively without changing TypeScript or adding JSX",
                fun _ ->
                    for name in [ "Component.tsx"; "Component.TSX" ] do
                        assertThat (resolveLanguageForFile name |> Option.map _.Id) (isEqualTo (Some "typescriptreact"))

                    assertThat
                        (resolveLanguageForFile "legacy.ts" |> Option.map _.GrammarPath)
                        (isEqualTo (Some "grammars/tree-sitter-typescript.wasm"))

                    assertThat (resolveLanguageForFile "Component.jsx" |> Option.map _.Id) (isEqualTo None)
            )
            test (
                "discovers TSX through explicit paths, directories, and globs",
                fun _ ->
                    let fixtureDirectory = cwd () + "/src/test/fixtures/tsx"
                    let fixturePath = fixtureDirectory + "/components.tsx"

                    for input in [ fixturePath; fixtureDirectory; fixtureDirectory + "/**/*.tsx" ] do
                        assertThat (resolveSupportedFiles [ input ] fixtureDirectory) (isEqualTo [ Path fixturePath ])
            )
            testAsync (
                "parses JSX and generic arrows and analyzes expressions without counting markup nesting",
                fun _ ->
                    toAsync (
                        task {
                            let! source, tree = parseFixture TypeScript.tsxLanguageAdapter "tsx/components.tsx"
                            assertThat (nodeHasError tree) isFalse
                            let views = callables tree
                            let clean = views |> List.find (fun view -> view.BindingName = Some "CleanMarkup")
                            assertThat (complexityOf TypeScript.tsxLanguageAdapter clean.Anchor) (isEqualTo 1)

                            let expression =
                                views |> List.find (fun view -> view.BindingName = Some "ExpressionComponent")

                            assertThat (complexityOf TypeScript.tsxLanguageAdapter expression.Anchor) (isEqualTo 2)

                            let callback =
                                views
                                |> List.find (fun view -> (nodeText view.Anchor).StartsWith("item => item ?"))

                            assertThat (complexityOf TypeScript.tsxLanguageAdapter callback.Anchor) (isEqualTo 2)

                            let findings =
                                analyzeFixture source tree TypeScript.tsxLanguageAdapter "components.tsx"

                            assertValidPositions findings source

                            for kind in [ Complexity; Parameters; OpaqueBoolean ] do
                                assertThat (findings |> List.exists (fun finding -> finding.Type = kind)) isTrue

                            let cleanRange = findFunctionRange source (FunctionName "CleanMarkup")
                            assertThat (violationsIn findings cleanRange |> List.length) (isEqualTo 0)
                        }
                    )
            )
            testAsync (
                "loads distinct TypeScript and TSX editor parsers and retains angle-bracket assertions in TS",
                fun _ ->
                    toAsync (
                        task {
                            do! initializeParser ()

                            let context: GrammarContext =
                                {
                                    ExtensionPath = cwd ()
                                    LoadedLanguages = Dictionary()
                                    InFlightLoads = Dictionary()
                                }

                            let! ts = getOrLoadLanguage "typescript" context
                            let! tsx = getOrLoadLanguage "typescriptreact" context

                            match ts, tsx with
                            | Ok(Some plain), Ok(Some react) ->
                                assertThat plain.Adapter.Id (isEqualTo "typescript")
                                assertThat react.Adapter.Id (isEqualTo "typescriptreact")

                                assertThat
                                    (withParsedTree plain.Parser "const value = <string>input;" nodeHasError)
                                    isFalse

                                assertThat (withParsedTree react.Parser "const view = <div />;" nodeHasError) isFalse
                            | _ -> failwith "Expected both editor grammars to load"

                            assertThat context.LoadedLanguages.Count (isEqualTo 2)
                        }
                    )
            )
        ]
    )
