module Energy.Languages.Registry

open System
open Energy.Core.LanguageAdapter
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin
open Energy.Languages.CPlusPlus

// decision: the registry is keyed by VS Code language id; the extension receives that canonical
// identifier directly, while the CLI resolves an extension through the same registry below.
let languages: Map<string, LanguageAdapter> =
    [ "python", pythonLanguageAdapter
      "fsharp", fSharpLanguageAdapter
      "typescript", typeScriptLanguageAdapter
      "kotlin", kotlinLanguageAdapter
      "cpp", cPlusPlusLanguageAdapter ]
    |> Map.ofList

// decision: keep suffixes instead of extracting only the final extension so compound C++ template
// names such as `config.hpp.in` retain their language identity. Longest-first matching makes this
// deterministic if future suffixes overlap, while ordinal case-insensitive comparison preserves the
// CLI's current case-insensitive behavior without locale-dependent filename rules.
let private suffixToLanguageId =
    [ ".py", "python"
      ".fs", "fsharp"
      ".fsx", "fsharp"
      ".fsi", "fsharp"
      ".ts", "typescript"
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
      ".h.in", "cpp" ]
    |> List.sortByDescending (fun (suffix, _) -> suffix.Length)

let tryFind languageId = Map.tryFind languageId languages

let resolveLanguageForFile (fileName: string) =
    suffixToLanguageId
    |> List.tryFind (fun (suffix, _) -> fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
    |> Option.map snd
    |> Option.bind tryFind
