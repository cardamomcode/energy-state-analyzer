// # LoadedLanguage
//
// A loaded language pairs an adapter with a configured parser so callers cannot accidentally analyze with
// unrelated grammar policy. Grammar.fs creates and caches these pairs; Analysis.fs borrows them for
// synchronous parsing and releases each resulting tree after analysis.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Extension.LoadedLanguage

open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter

/// A grammar adapter paired with the parser that owns its loaded grammar.
type LoadedLanguage =
    {
        Adapter: LanguageAdapter
        Parser: Parser
    }
