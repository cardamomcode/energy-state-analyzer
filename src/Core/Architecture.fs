module Energy.Core.Architecture

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter
open Energy.Core.ArchitectureModel

let private isImportNode language node =
    [ language.NodeTypes.ImportStatement; language.NodeTypes.ImportFromStatement ]
    |> List.choose id
    |> List.contains (nodeType node)

/// Extract source-level imports without attempting language-specific name or type resolution.
// decision: preserves adapters' raw dependency sources because they are dependable across all five
// grammars; call resolution and inferred dependency edges would overstate what this analyzer knows.
let collectImports (filePath: string) (language: LanguageAdapter) (tree: Node) : RepositoryImport list =
    let rec visit node =
        let own =
            if isImportNode language node then
                let position = nodeStartPosition node

                language.ImportInfo node
                |> List.map (fun importInfo ->
                    { FilePath = filePath
                      Line = position.Row
                      Column = position.Column
                      Source = importInfo.Source })
            else
                []

        own @ (nodeChildren node |> List.collect visit)

    visit tree

let private normalize (value: string) = value.Replace("\\", "/").TrimEnd('/')

let private pathMatches prefix filePath =
    let normalizedPath = normalize filePath

    normalizedPath = prefix
    || normalizedPath.StartsWith(prefix + "/", System.StringComparison.Ordinal)

let private importMatches prefix source =
    let normalizedSource = normalize source

    normalizedSource = prefix
    || normalizedSource.StartsWith(prefix + ".", System.StringComparison.Ordinal)
    || normalizedSource.StartsWith(prefix + "/", System.StringComparison.Ordinal)

let private findFileZone policy filePath =
    policy.Zones
    |> List.tryFind (fun zone -> zone.Files |> List.exists (fun prefix -> pathMatches prefix filePath))

let private findImportZone policy source =
    policy.Zones
    |> List.tryFind (fun zone -> zone.ImportPrefixes |> List.exists (fun prefix -> importMatches prefix source))

/// Evaluate only configured, directly observable layer dependencies.
let audit (policy: ArchitecturePolicy) (filesScanned: int) (imports: RepositoryImport list) : ArchitectureReport =
    let resolved, unresolved =
        imports
        |> List.partition (fun item ->
            findFileZone policy item.FilePath |> Option.isSome
            && findImportZone policy item.Source |> Option.isSome)

    let violations =
        resolved
        |> List.choose (fun item ->
            match findFileZone policy item.FilePath, findImportZone policy item.Source with
            | Some fromZone, Some toZone when
                policy.ForbiddenDependencies
                |> List.contains
                    { From = fromZone.Name
                      To = toZone.Name }
                ->
                Some
                    { Import = item
                      FromZone = fromZone.Name
                      ToZone = toZone.Name }
            | _ -> None)

    { FilesScanned = filesScanned
      Imports = imports
      UnresolvedImports = unresolved
      Violations = violations }
