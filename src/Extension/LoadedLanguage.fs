module Energy.Extension.LoadedLanguage

open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter

/// A grammar adapter paired with the parser that owns its loaded grammar.
type LoadedLanguage =
    {
        Adapter: LanguageAdapter
        Parser: Parser
    }
