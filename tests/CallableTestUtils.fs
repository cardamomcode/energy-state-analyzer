// # CallableTestUtils
//
// Callable tests share a recursive collector of adapter-normalized views and source-range assertions.
// Anchors and bodies must reproduce their exact source slices, and bodies must not precede their anchors.
// AnonymousCallableTests.fs and the language suites use these checks to guard grammar normalization.
//
// Reading path: [Source guide](../docs/source-guide.md).

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
    /// Assert that a live node's start and exclusive end select exactly its reported source text.
    let assertRange (node: Node) =
        let startIndex = nodeStartIndex node
        let length = nodeEndIndex node - startIndex
        assertThat (sourceCode.Substring(startIndex, length)) (isEqualTo (nodeText node))

    for callable in callables do
        assertRange callable.Anchor
        assertRange callable.Body
        assertThat (nodeStartIndex callable.Body) (isGreaterOrEqual (nodeStartIndex callable.Anchor))
