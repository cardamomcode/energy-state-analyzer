module Energy.Core.Detectors.Coherence


open Energy.Core
open Energy.Core.Config

// The "Utils/Helpers Sprawl" detector.
//
// Unlike every other detector here, coherence is a *whole-file* metric: it flags files that have lost
// their cohesion by having too many unrelated functions, too many large functions, or too many
// distinct imports — and its class-relatedness sub-check for OOP files. It has no per-line heatmap;
// each violation is anchored at the most directly actionable position (first function / first import /
// first large function) rather than line 0.

// decision: coherence thresholds live in Core.Config as the single source of truth; this detector
// reads them from ctx.Options (and its own `thresholds` parameter) so it no longer re-exports a
// module-level copy.

let private utilsFileFunctionThreshold = 8
let private genericFunctionCountThreshold = 12
let private highFunctionCountThreshold = 15
let private largeFunctionSeverityMultiplier = 1.5

// decision: methods are grouped by their nearest enclosing class rather than folded into the same flat
// function list a free-standing function would land in — a class is already a cohesion boundary of its
// own (see checkClassRelatedness), so its method count isn't this detector's function-count-sprawl
// concern. A method with no enclosing class (every function in a functional-style module) still lands in
// `freeFunctions`, preserving this detector's existing behavior for non-OOP files untouched.
type private Collected =
    { FreeFunctions: TreeSitter.Node list
      Classes: ClassRelatedness.ClassInfo list
      Imports: LanguageAdapter.ImportInfo list
      FirstImportNode: TreeSitter.Node option }

// decision: the traversal accumulates into an immutable Collected record threaded through the
// recursion (merge keeps the earliest first-import node, since children are processed in source
// order), instead of mutating captured state — same result, no shared mutable buffers.
let private collectFunctionsClassesAndImports
    (tree: TreeSitter.Node)
    (language: LanguageAdapter.LanguageAdapter)
    : Collected =
    // decision: requires isNamed, not just a type match — Kotlin's import rule is literally named `import`,
    // which collides with the anonymous `import` keyword token that is itself a child of every import node
    // (node.type for an anonymous node is its literal text). Without this guard, every Kotlin import is
    // counted twice: once for the named node, once for its own leading keyword token.
    let isImportNode (node: TreeSitter.Node) : bool =
        let t = TreeSitter.nodeType node

        language.NodeTypes.ImportStatement |> Option.exists (fun nt -> t = nt)
        || language.NodeTypes.ImportFromStatement |> Option.exists (fun nt -> t = nt)

    let empty: Collected =
        { FreeFunctions = []
          Classes = []
          Imports = []
          FirstImportNode = None }

    let merge (left: Collected) (right: Collected) : Collected =
        { FreeFunctions = left.FreeFunctions @ right.FreeFunctions
          Classes = left.Classes @ right.Classes
          Imports = left.Imports @ right.Imports
          FirstImportNode = Option.orElse left.FirstImportNode right.FirstImportNode }

    // invariant: every syntax node is traversed exactly once; a class replaces the inherited class
    // context for its subtree so its methods never leak into FreeFunctions.
    let rec traverse (node: TreeSitter.Node) (enclosingClass: ClassRelatedness.ClassInfo option) : Collected =
        let currentClass =
            if language.IsClassDefinition node then
                let classInfo: ClassRelatedness.ClassInfo =
                    { Name = language.GetClassName node
                      Node = node
                      BaseNames = language.GetBaseClassNames node
                      Methods = ResizeArray<TreeSitter.Node>() }

                Some classInfo
            else
                None

        let own =
            match currentClass with
            | Some cls -> { empty with Classes = [ cls ] }
            | None when language.IsFunctionDefinition node ->
                match enclosingClass with
                | Some cls ->
                    cls.Methods.Add(node) |> ignore
                    empty
                | None -> { empty with FreeFunctions = [ node ] }
            | None when isImportNode node ->
                let imports =
                    language.ImportInfo node
                    |> List.map (fun importInfo ->
                        if System.String.IsNullOrEmpty importInfo.Source then
                            { importInfo with
                                Source = TreeSitter.nodeText node }
                        else
                            importInfo)

                { empty with
                    Imports = imports
                    FirstImportNode = Some node }
            | None -> empty

        let childClass = Option.orElse currentClass enclosingClass

        TreeSitter.nodeChildren node
        |> List.map (fun child -> traverse child childClass)
        |> List.fold merge own

    traverse tree None

// decision: a confirmed type signal (result is Measured, not InsufficientData) is authoritative and
// short-circuits the naming heuristic entirely — both for a confirmed shared type (Result === true, e.g.
// an F#-style module of one-verb-per-operation functions sharing no name prefix at all) and for confirmed
// type diversity (Result === false), which must NOT be overridden by a coincidentally shared name prefix.
// The naming heuristic only runs when the type signal is InsufficientData (too little type coverage to trust).
let private isCohesiveByNamingOrType
    (functions: TreeSitter.Node list)
    (thresholds: CoherenceThresholds)
    (typeResult: TypeCohesion.TypeCohesionResult)
    : bool =
    match typeResult with
    | TypeCohesion.Measured r -> r.Result
    | TypeCohesion.InsufficientData -> NamingCohesion.looksLikeSingleDomain functions thresholds.SingleDomainNameShare

// decision: anchored on the first function in the file (source order) rather than line 0 — there's no
// single "worst offender" for a whole-file count signal, but pointing at the first function at least lands
// the reader inside the file instead of at a meaningless (0, 0).
let private functionCountViolation
    (functionCount: int)
    (message: string)
    (position: Position.Position)
    : Violation.EnergyViolation =
    { Line = position.Line
      Column = position.Column
      Type = Violation.Coherence
      Severity =
        if functionCount > highFunctionCountThreshold then
            Violation.High
        else
            Violation.Medium
      Message = message
      Hotspots = [] }

// Flag files with too many unrelated functions (utils/helpers sprawl).
// decision: lowers the flagging threshold from 12 to 8 functions when the filename itself signals a
// grab-bag module (util/helper/common) — the name is treated as a proxy for "already known to lack a
// single responsibility". Only ever sees free-standing functions, not class methods.
let private checkFunctionCountSprawl
    (functions: TreeSitter.Node list)
    (fileName: string)
    (thresholds: CoherenceThresholds)
    (language: LanguageAdapter.LanguageAdapter)
    (positions: Position.PositionLookup)
    : Violation.EnergyViolation option =
    if functions.Length <= utilsFileFunctionThreshold then
        None
    else
        let isUtilsFile = NamingCohesion.isUtilsFileName fileName

        let typeResult =
            TypeCohesion.typeCohesionResult
                functions
                language
                { MaxDiversityRatio = thresholds.MaxTypeDiversityRatio
                  MinCoverage = thresholds.MinTypedCoverage }
        // decision: an explicit utils/helper/common filename overrides either cohesion signal (naming or
        // type) — a module that already admits to being a grab-bag in its own name doesn't get to argue its
        // way out via consistent prefixes or a shared type.
        let singleDomain =
            (not isUtilsFile) && isCohesiveByNamingOrType functions thresholds typeResult

        if
            (not isUtilsFile)
            && (functions.Length <= genericFunctionCountThreshold || singleDomain)
        then
            None
        else
            let position = positions.toPosition (TreeSitter.nodeStartIndex functions.[0])

            match typeResult with
            // decision: once a file is already going to be flagged at the existing thresholds, a
            // confidently-diverse type result is authoritative over naming and gets the stronger, more
            // specific message below instead of the generic one. A lower threshold was tried and rejected
            // after dogfooding surfaced a real false positive on this project's own coherence.ts (9 small,
            // purpose-cohesive helper functions), showing type-diversity isn't reliable enough below ~12
            // functions to tell a legitimately-typed small module apart from a real grab-bag.
            | TypeCohesion.Measured r when not r.Result ->
                Some(
                    functionCountViolation
                        functions.Length
                        (sprintf
                            "File coherence warning: %d functions in one file spanning %d unrelated types. This is a stronger sprawl signal than function count alone — the functions don't share a common domain type, so moving them into existing cohesive modules (grouped by the type they operate on) is likely to help more than an arbitrary split."
                            functions.Length
                            r.DistinctTypes)
                        position
                )
            | _ ->
                Some(
                    functionCountViolation
                        functions.Length
                        (sprintf
                            "File coherence warning: %d functions in one file. If they belong to distinct domains, prefer moving them into existing cohesive modules; splitting into a new file only helps if it doesn't just relocate the same imports/coupling."
                            functions.Length)
                        position
                )

let private lineCount (node: TreeSitter.Node) : int =
    TreeSitter.nodeEndRow node - TreeSitter.nodeStartRow node + 1

// Flag files with too many large functions, regardless of total function count — a module with 30 small
// functions is fine, one with 6 sprawling ones isn't. Anchored on the first large function in source order.
let private checkLargeFunctionSprawl
    (functions: TreeSitter.Node list)
    (thresholds: CoherenceThresholds)
    (positions: Position.PositionLookup)
    : Violation.EnergyViolation option =
    let largeFunctions =
        functions
        |> List.filter (fun fn -> lineCount fn > thresholds.LargeFunctionLines)

    if largeFunctions.Length <= thresholds.MaxLargeFunctions then
        None
    else
        let position = positions.toPosition (TreeSitter.nodeStartIndex largeFunctions.[0])

        Some
            { Line = position.Line
              Column = position.Column
              Type = Violation.Coherence
              Severity =
                if
                    float largeFunctions.Length > float thresholds.MaxLargeFunctions * largeFunctionSeverityMultiplier
                then
                    Violation.High
                else
                    Violation.Medium
              Message =
                sprintf
                    "%d functions exceed %d lines. Large functions carry more complexity than function count alone suggests."
                    largeFunctions.Length
                    thresholds.LargeFunctionLines
              Hotspots = [] }

// The "Utils/Helpers Sprawl" detector. Methods are grouped by enclosing class (see
// collectFunctionsClassesAndImports), so the function-count sprawl check only sees free-standing
// functions; class methods are judged separately by checkClassRelatedness.
let analyzeFileCoherence (ctx: Context.AnalysisContext) : Context.AnalysisContext =
    let collected = collectFunctionsClassesAndImports ctx.Tree ctx.Language
    let FreeFunctions = collected.FreeFunctions
    let Classes = collected.Classes
    let Imports = collected.Imports
    let FirstImportNode = collected.FirstImportNode
    // decision: the large-function check considers both free-standing functions and class methods; the
    // function-count sprawl check considers only free-standing ones.
    let allFunctions =
        FreeFunctions @ (Classes |> List.collect (fun c -> List.ofSeq c.Methods))

    let findings =
        [ checkFunctionCountSprawl FreeFunctions ctx.FileName ctx.Options.Coherence ctx.Language ctx.Positions
          checkLargeFunctionSprawl allFunctions ctx.Options.Coherence ctx.Positions
          ImportCoherence.check Imports FirstImportNode ctx.Language ctx.Positions
          ClassRelatedness.checkClassRelatedness
              Classes
              ctx.Options.Coherence.SingleDomainNameShare
              ctx.Language
              ctx.Positions ]
        |> List.choose id

    Context.addViolations findings ctx

let detector: Context.Detector =
    { Name = "coherence"
      Run = analyzeFileCoherence }
