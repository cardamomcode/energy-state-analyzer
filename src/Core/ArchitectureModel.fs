module Energy.Core.ArchitectureModel

/// A named repository area, matched against scanned files and import sources.
type ArchitectureZone =
    { Name: string
      Files: string list
      ImportPrefixes: string list }

type ForbiddenDependency = { From: string; To: string }

type ArchitecturePolicy =
    { Zones: ArchitectureZone list
      ForbiddenDependencies: ForbiddenDependency list }

type RepositoryImport =
    { FilePath: string
      Line: int
      Column: int
      Source: string }

type ArchitectureViolation =
    { Import: RepositoryImport
      FromZone: string
      ToZone: string }

type ArchitectureReport =
    { FilesScanned: int
      Imports: RepositoryImport list
      UnresolvedImports: RepositoryImport list
      Violations: ArchitectureViolation list }
