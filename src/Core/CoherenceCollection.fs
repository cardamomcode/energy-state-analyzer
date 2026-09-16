module Energy.Core.CoherenceCollection

open Energy.Core.Detectors

/// Functions, classes, and imports collected for whole-file coherence scoring.
///
/// decision: methods are grouped by their nearest enclosing class instead of entering the free-function
/// list because a class is already a responsibility boundary judged by class-relatedness and god-class.
type Collected =
    { FreeFunctions: LanguageAdapter.CallableView list
      Classes: ClassRelatedness.ClassInfo list
      Imports: LanguageAdapter.ImportInfo list
      FirstImportNode: TreeSitter.Node option }

/// Report whether a node is an import statement in the current language.
let private isImportNode (language: LanguageAdapter.LanguageAdapter) (node: TreeSitter.Node) : bool =
    // decision: requires a named-node type match because Kotlin's anonymous `import` keyword token
    // has the same literal type as its enclosing named import rule.
    let nodeType = TreeSitter.nodeType node

    language.NodeTypes.ImportStatement |> Option.exists ((=) nodeType)
    || language.NodeTypes.ImportFromStatement |> Option.exists ((=) nodeType)

/// Empty collection state for a subtree with no coherence signals.
let private empty: Collected =
    { FreeFunctions = []
      Classes = []
      Imports = []
      FirstImportNode = None }

/// Merge sibling subtree results while preserving source order and the earliest import anchor.
let private merge (left: Collected) (right: Collected) : Collected =
    { FreeFunctions = left.FreeFunctions @ right.FreeFunctions
      Classes = left.Classes @ right.Classes
      Imports = left.Imports @ right.Imports
      FirstImportNode = Option.orElse left.FirstImportNode right.FirstImportNode }

/// Add a direct module responsibility to the free-function collection.
let private collectFree (callable: LanguageAdapter.CallableView) : Collected =
    { empty with
        FreeFunctions = [ callable ] }

/// Add a direct class responsibility when the language models its enclosing class.
let private collectClass
    (enclosingClass: ClassRelatedness.ClassInfo option)
    (callable: LanguageAdapter.CallableView)
    : Collected =
    match enclosingClass with
    | Some cls ->
        cls.Methods.Add(callable)
        empty
    | None -> empty

/// Route one callable to the responsibility boundary selected by its normalized role.
///
/// decision: only named definitions and direct module/class bindings participate in coherence;
/// callbacks, returned closures, and function-local callable variables remain implementation detail.
let private collectCallable enclosingClass (callable: LanguageAdapter.CallableView) =
    match callable.Role, enclosingClass with
    | LanguageAdapter.NamedDefinition, Some _ -> collectClass enclosingClass callable
    | LanguageAdapter.NamedDefinition, None -> collectFree callable
    | LanguageAdapter.BoundAnonymous LanguageAdapter.ModuleBinding, _ -> collectFree callable
    | LanguageAdapter.BoundAnonymous LanguageAdapter.ClassMemberBinding, _ -> collectClass enclosingClass callable
    | LanguageAdapter.InlineAnonymous, _ -> empty

/// Create class collection state when the current node establishes a modeled class boundary.
let private classAt (language: LanguageAdapter.LanguageAdapter) (node: TreeSitter.Node) =
    if language.IsClassDefinition node then
        let classInfo: ClassRelatedness.ClassInfo =
            { Name = language.GetClassName node
              Node = node
              BaseNames = language.GetBaseClassNames node
              Methods = ResizeArray<LanguageAdapter.CallableView>() }

        Some classInfo
    else
        None

/// Preserve an import's parsed source or fall back to its complete syntax text.
let private normalizeImportSource (node: TreeSitter.Node) (importInfo: LanguageAdapter.ImportInfo) =
    if System.String.IsNullOrEmpty importInfo.Source then
        { importInfo with
            Source = TreeSitter.nodeText node }
    else
        importInfo

/// Collect the class or import established by the current syntax node.
let private collectStructure language node currentClass : Collected =
    match currentClass with
    | Some cls -> { empty with Classes = [ cls ] }
    | None when isImportNode language node ->
        { empty with
            Imports = language.ImportInfo node |> List.map (normalizeImportSource node)
            FirstImportNode = Some node }
    | None -> empty

/// Recursively collect coherence responsibilities and structural signals exactly once per node.
///
/// invariant: a class replaces inherited class context for its subtree, so its methods never leak
/// into FreeFunctions.
let rec private traverse language node enclosingClass : Collected =
    let currentClass = classAt language node
    let childClass = Option.orElse currentClass enclosingClass

    let own =
        language.GetCallableViews node
        |> List.map (collectCallable enclosingClass)
        |> List.fold merge (collectStructure language node currentClass)

    TreeSitter.nodeChildren node
    |> List.map (fun child -> traverse language child childClass)
    |> List.fold merge own

/// Collect all inputs needed by file-coherence scoring from one parsed syntax tree.
let collect (tree: TreeSitter.Node) (language: LanguageAdapter.LanguageAdapter) : Collected =
    traverse language tree None
