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

// Coherence thresholds are read from shared config rather than re-exported here, so there is one
// definition of every threshold value.
//
// decision: coherence thresholds live in Core.Config as the single source of truth; this detector
// reads every one from the CoherenceThresholds record passed to each check (ctx.Options.Coherence at
// the entry point, threaded down into the private checks) so it no longer re-exports a module-level
// copy. The function-count sprawl thresholds (utils/generic/high), the large-function line count and
// count bar, and the severity multiplier are all configured the same way as the import signals.

/// Decide cohesion from the type signal when it is confirmed, else fall back to the naming heuristic.
///
/// decision: a confirmed type signal (result is Measured, not InsufficientData) is authoritative and
/// short-circuits the naming heuristic entirely — both for a confirmed shared type (Result === true, e.g.
/// an F#-style module of one-verb-per-operation functions sharing no name prefix at all) and for confirmed
/// type diversity (Result === false), which must NOT be overridden by a coincidentally shared name prefix.
/// The naming heuristic only runs when the type signal is InsufficientData (too little type coverage to trust).
let private isCohesiveByNamingOrType
    (functions: LanguageAdapter.CallableView list)
    (thresholds: CoherenceThresholds)
    (typeResult: TypeCohesion.TypeCohesionResult)
    : bool =
    match typeResult with
    | TypeCohesion.Measured r -> r.Result
    | TypeCohesion.InsufficientData -> NamingCohesion.looksLikeSingleDomain functions thresholds.SingleDomainNameShare

/// Build a coherence violation anchored at a single position with severity chosen from the function count.
///
/// decision: anchored on the first function in the file (source order) rather than line 0 — there's no
/// single "worst offender" for a whole-file count signal, but pointing at the first function at least lands
/// the reader inside the file instead of at a meaningless (0, 0).
let private functionCountViolation
    (functionCount: int)
    (message: string)
    (position: Position.Position)
    (thresholds: CoherenceThresholds)
    : Violation.EnergyViolation =
    {
        Line = position.Line
        Column = position.Column
        Type = Violation.Coherence
        Severity =
            if functionCount > thresholds.HighFunctionCount then
                Violation.High
            else
                Violation.Medium
        Message = message
        Hotspots = []
    }

/// Flag files with too many unrelated functions (utils/helpers sprawl).
/// decision: lowers the flagging threshold from 12 to 8 functions when the filename itself signals a
/// grab-bag module (util/helper/common) — the name is treated as a proxy for "already known to lack a
/// single responsibility". Only ever sees free-standing functions, not class methods.
let private checkFunctionCountSprawl
    (functions: LanguageAdapter.CallableView list)
    (fileName: string)
    (thresholds: CoherenceThresholds)
    (language: LanguageAdapter.LanguageAdapter)
    (positions: Position.PositionLookup)
    : Violation.EnergyViolation option =
    if functions.Length <= thresholds.UtilsFileFunctionCount then
        None
    else
        let isUtilsFile = NamingCohesion.isUtilsFileName fileName

        let typeResult =
            TypeCohesion.typeCohesionResult
                functions
                language
                {
                    MaxDiversityRatio = thresholds.MaxTypeDiversityRatio
                    MinCoverage = thresholds.MinTypedCoverage
                }
        // decision: an explicit utils/helper/common filename overrides either cohesion signal (naming or
        // type) — a module that already admits to being a grab-bag in its own name doesn't get to argue its
        // way out via consistent prefixes or a shared type.
        let singleDomain =
            (not isUtilsFile) && isCohesiveByNamingOrType functions thresholds typeResult

        if
            (not isUtilsFile)
            && (functions.Length <= thresholds.GenericFunctionCount || singleDomain)
        then
            None
        else
            let position = positions.toPosition (TreeSitter.nodeStartIndex functions.[0].Anchor)

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
                        thresholds
                )
            | _ ->
                Some(
                    functionCountViolation
                        functions.Length
                        (sprintf
                            "File coherence warning: %d functions in one file. If they belong to distinct domains, prefer moving them into existing cohesive modules; splitting into a new file only helps if it doesn't just relocate the same imports/coupling."
                            functions.Length)
                        position
                        thresholds
                )

/// Count a callable's source lines from its anchor to the end of its body.
///
/// decision: tree-sitter-fsharp attaches the FOLLOWING binding's xml_doc block to the end of the
/// previous declaration_expression, so the raw body end row counts another function's doc comment
/// as this function's lines — adding doc lines above one function used to resize its neighbor and
/// push it across the large-function threshold (guarded by the DocCommentBoundary fixtures).
/// Skip trailing documentation nodes when locating the body's last line.
let private lineCount (callable: LanguageAdapter.CallableView) : int =
    let lastCodeChild =
        TreeSitter.nodeChildren callable.Body
        |> List.rev
        |> List.tryFind (fun c -> TreeSitter.nodeType c <> TreeSitter.NodeType "xml_doc")

    let bodyEndRow =
        match lastCodeChild with
        | Some child -> TreeSitter.nodeEndRow child
        | None -> TreeSitter.nodeEndRow callable.Body

    bodyEndRow - TreeSitter.nodeStartRow callable.Anchor + 1

/// Flag files with too many large functions, regardless of total function count — a module with 30 small
/// functions is fine, one with 6 sprawling ones isn't. Anchored on the first large function in source order.
let private checkLargeFunctionSprawl
    (functions: LanguageAdapter.CallableView list)
    (thresholds: CoherenceThresholds)
    (positions: Position.PositionLookup)
    : Violation.EnergyViolation option =
    let largeFunctions =
        functions
        |> List.filter (fun fn -> lineCount fn > thresholds.LargeFunctionLines)

    if largeFunctions.Length <= thresholds.MaxLargeFunctions then
        None
    else
        let position =
            positions.toPosition (TreeSitter.nodeStartIndex largeFunctions.[0].Anchor)

        Some
            {
                Line = position.Line
                Column = position.Column
                Type = Violation.Coherence
                Severity =
                    if
                        float largeFunctions.Length >
                            float thresholds.MaxLargeFunctions * thresholds.LargeFunctionSeverityMultiplier
                    then
                        Violation.High
                    else
                        Violation.Medium
                Message =
                    sprintf
                        "%d functions exceed %d lines. Large functions carry more complexity than function count alone suggests."
                        largeFunctions.Length
                        thresholds.LargeFunctionLines
                Hotspots = []
            }

/// The "Utils/Helpers Sprawl" detector. Methods are grouped by enclosing class (see
/// collectFunctionsClassesAndImports), so the function-count sprawl check only sees free-standing
/// functions; class methods are judged separately by checkClassRelatedness.
let analyzeFileCoherence (ctx: Context.AnalysisContext) : Context.AnalysisContext =
    let collected = CoherenceCollection.collect ctx.Tree ctx.Language
    let FreeFunctions = collected.FreeFunctions
    let Classes = collected.Classes
    let Imports = collected.Imports
    let FirstImportNode = collected.FirstImportNode
    // decision: the large-function check considers both free-standing functions and class methods; the
    // function-count sprawl check considers only free-standing ones.
    let allFunctions =
        FreeFunctions @ (Classes |> List.collect (fun c -> List.ofSeq c.Methods))

    let findings =
        [
            checkFunctionCountSprawl FreeFunctions ctx.FileName ctx.Options.Coherence ctx.Language ctx.Positions
            checkLargeFunctionSprawl allFunctions ctx.Options.Coherence ctx.Positions
            ImportCoherence.check Imports FirstImportNode ctx.Language ctx.Positions ctx.Options.Coherence
            ClassRelatedness.checkClassRelatedness
                Classes
                ctx.Options.Coherence.SingleDomainNameShare
                ctx.Language
                ctx.Positions
            // God-class is the class-level counterpart: one type whose methods span too many unrelated
            // domains (as opposed to checkClassRelatedness, which is several unrelated types per file).
            ClassRelatedness.checkGodClass
                Classes
                {
                    Language = ctx.Language
                    Thresholds = ctx.Options.Coherence
                    Positions = ctx.Positions
                }
        ]
        |> List.choose id

    Context.addViolations findings ctx

let detector: Context.Detector =
    {
        Name = "coherence"
        Run = analyzeFileCoherence
    }
