// # Registry
//
// Hosts share this registry of supported language adapters. Editor IDs select adapters directly; CLI
// filenames use longest-first, case-insensitive suffix matching, including compound C++ suffixes.
// Unsupported input returns None so hosts can choose skip or error behavior. Each adapter implements
// ../Core/LanguageAdapter.fs.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Languages.Registry

open System
open Energy.Core.LanguageAdapter

/// Register every supported language adapter, keyed by VS Code language id.
///
/// decision: the registry is keyed by VS Code language id; the extension receives that canonical
/// identifier directly, while the CLI resolves an extension through the same registry below.
let languages: Map<string, LanguageAdapter> =
    [
        "python", Python.pythonLanguageAdapter
        "fsharp", FSharp.fSharpLanguageAdapter
        "typescript", TypeScript.typeScriptLanguageAdapter
        "typescriptreact", TypeScript.tsxLanguageAdapter
        "kotlin", Kotlin.kotlinLanguageAdapter
        "cpp", CPlusPlus.cPlusPlusLanguageAdapter
        "csharp", CSharp.cSharpLanguageAdapter
        "erlang", Erlang.erlangLanguageAdapter
        "rust", Rust.rustLanguageAdapter
    ]
    |> Map.ofList

/// Map a file's trailing suffix to its language id, longest suffix first.
///
/// decision: keep suffixes instead of extracting only the final extension so compound C++ template
/// names such as `config.hpp.in` retain their language identity. Longest-first matching makes this
/// deterministic if future suffixes overlap, while ordinal case-insensitive comparison preserves the
/// CLI's current case-insensitive behavior without locale-dependent filename rules.
let private suffixToLanguageId =
    [
        ".py", "python"
        ".fs", "fsharp"
        ".fsx", "fsharp"
        ".fsi", "fsharp"
        ".ts", "typescript"
        ".tsx", "typescriptreact"
        ".cs", "csharp"
        ".csx", "csharp"
        ".erl", "erlang"
        ".hrl", "erlang"
        ".rs", "rust"
        ".kt", "kotlin"
        ".kts", "kotlin"
        ".cpp", "cpp"
        ".cppm", "cpp"
        ".cc", "cpp"
        ".ccm", "cpp"
        ".cxx", "cpp"
        ".cxxm", "cpp"
        ".c++", "cpp"
        ".c++m", "cpp"
        ".hpp", "cpp"
        ".hh", "cpp"
        ".hxx", "cpp"
        ".h++", "cpp"
        ".h", "cpp"
        ".ii", "cpp"
        ".ino", "cpp"
        ".inl", "cpp"
        ".ipp", "cpp"
        ".ixx", "cpp"
        ".mpp", "cpp"
        ".mxx", "cpp"
        ".tpp", "cpp"
        ".txx", "cpp"
        ".hpp.in", "cpp"
        ".h.in", "cpp"
    ]
    |> List.sortByDescending (fun (suffix, _) -> suffix.Length)

/// Look up an adapter by exact editor language ID, returning None for unsupported IDs.
let tryFind languageId = Map.tryFind languageId languages

/// Resolve the longest recognized filename suffix using ordinal case-insensitive comparison, then return
/// its adapter; unsupported names return None.
let resolveLanguageForFile (fileName: string) =
    suffixToLanguageId
    |> List.tryFind (fun (suffix, _) -> fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
    |> Option.map snd
    |> Option.bind tryFind
