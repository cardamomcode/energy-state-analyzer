module Energy.Languages.Registry

open System
open Energy.Core.LanguageAdapter
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin

// decision: the registry is keyed by VS Code language id; the extension receives that canonical
// identifier directly, while the CLI resolves an extension through the same registry below.
let languages: Map<string, LanguageAdapter> =
    [ "python", PYTHON
      "fsharp", FSHARP
      "typescript", TYPESCRIPT
      "kotlin", KOTLIN ]
    |> Map.ofList

let private extensionToLanguageId =
    [ ".py", "python"
      ".fs", "fsharp"
      ".fsx", "fsharp"
      ".fsi", "fsharp"
      ".ts", "typescript"
      ".kt", "kotlin"
      ".kts", "kotlin" ]
    |> Map.ofList

let tryFind languageId = Map.tryFind languageId languages

let resolveLanguageForFile (fileName: string) =
    let extension =
        let index = fileName.LastIndexOf('.')

        if index < 0 then
            None
        else
            Some(fileName.Substring(index).ToLowerInvariant())

    extension
    |> Option.bind (fun ext -> Map.tryFind ext extensionToLanguageId)
    |> Option.bind tryFind
