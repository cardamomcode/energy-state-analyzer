module Energy.Tests.CallableTestUtils

open Scriptorium.Nib.Assertion

open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter

/// Collect every callable view in source order from a parsed tree.
let rec collectCallableViews (language: LanguageAdapter) (node: Node) =
    language.GetCallableViews node
    @ (nodeNamedChildren node |> List.collect (collectCallableViews language))

/// Assert that callable anchors and bodies map back to their exact source slices.
let assertCallableSourceRanges (sourceCode: string) (callables: CallableView list) =
    let assertRange (node: Node) =
        let startIndex = nodeStartIndex node
        let length = nodeEndIndex node - startIndex
        assertThat (sourceCode.Substring(startIndex, length)) (isEqualTo (nodeText node))

    for callable in callables do
        assertRange callable.Anchor
        assertRange callable.Body
        assertThat (nodeStartIndex callable.Body) (isGreaterOrEqual (nodeStartIndex callable.Anchor))
