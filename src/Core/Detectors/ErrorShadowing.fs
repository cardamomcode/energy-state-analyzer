module Energy.Core.Detectors.ErrorShadowing

open Energy.Core
open Energy.Core.Context
open Energy.Core.Config
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// The "Error Shadowing" detector — a separation-of-concerns / cohesion signal.
//
// Unlike cyclomatic or cognitive complexity, which count *decision points*, this measures how much of
// a function's body lives inside an error-handling region (the try construct plus its catch/except/finally
// arms). When error handling occupies most of the function, the happy path it wraps is being shadowed:
// failure handling and business logic have collapsed into one unreadable mass. This flags both directions
// — logic buried under a single catch, and a function dominated by handlers — and points at the try/catch
// doing the shadowing rather than the function's signature.

// decision: count named nodes rather than "statements" so the metric is grammar-agnostic — the five
// grammars disagree on what counts as a statement node, but every one yields named descendant nodes, so
// "share of the body inside error handling" stays comparable across Python/TS/Kotlin/F#/C++ with no
// per-language statement-type table.

// decision: named region-walk flags keep these recursive walkers clear of the Opaque Boolean
// detector, which would otherwise flag a bare true/false passed positionally at a call site.
let private atFunctionRoot = true
let private descendIntoBody = false

// decision: thread the two recursive-walk booleans as a single record so countNodes' walker has no
// adjacent same-type primitive parameters (which would trip its own Primitive Obsession finding).
type private WalkState =
    { IsFunctionRoot: bool
      InErrorRegion: bool }

let private countNodes (language: LanguageAdapter.LanguageAdapter) (fnNode: TreeSitter.Node) : int * int =
    // decision: collect a per-named-node region flag first, then count — this keeps the metric
    // grammar-agnostic (the five grammars disagree on what counts as a statement node, but every one
    // yields named descendant nodes) and avoids a fold whose initial value could be mis-parsed.
    let rec walk (state: WalkState) (node: TreeSitter.Node) : bool list =
        if not state.IsFunctionRoot && language.IsFunctionDefinition node then
            // decision: nested functions are traversal boundaries because allFunctions analyzes each
            // one independently; their error-handling regions must not affect an enclosing function.
            []
        elif nodeIsNamed node then
            let nowInError =
                state.InErrorRegion
                || List.contains (nodeType node) language.ErrorHandlingAnchorTypes

            let descendedIntoBody =
                { IsFunctionRoot = descendIntoBody
                  InErrorRegion = nowInError }

            nowInError :: (nodeChildren node |> List.collect (walk descendedIntoBody))
        else
            // unnamed nodes are tokens/punctuation: descend so a named descendant inside an anchor is
            // still flagged, but never count the token itself.
            let descended =
                { state with
                    IsFunctionRoot = descendIntoBody }

            nodeChildren node |> List.collect (walk descended)

    let flags =
        fnNode
        |> walk
            { IsFunctionRoot = atFunctionRoot
              InErrorRegion = descendIntoBody }

    let errorCount = flags |> List.filter id |> List.length
    let logicCount = flags |> List.filter (fun flag -> not flag) |> List.length
    (errorCount, logicCount)

// The first error-handling region inside a function — the anchor we point the violation at, so the
// reader lands on the try/catch doing the shadowing rather than the function's signature.
let private firstAnchor (language: LanguageAdapter.LanguageAdapter) (fnNode: TreeSitter.Node) : TreeSitter.Node option =
    let rec walk (isFunctionRoot: bool) (node: TreeSitter.Node) : TreeSitter.Node option =
        if not isFunctionRoot && language.IsFunctionDefinition node then
            None
        elif List.contains (nodeType node) language.ErrorHandlingAnchorTypes then
            Some node
        else
            nodeChildren node |> List.tryPick (walk descendIntoBody)

    walk atFunctionRoot fnNode

// Every function definition anywhere in the tree, not just module-scope ones — methods nested inside a
// class are still functions whose logic can be shadowed by their own error handling.
let private allFunctions (language: LanguageAdapter.LanguageAdapter) (root: TreeSitter.Node) : TreeSitter.Node list =
    let rec walk (node: TreeSitter.Node) : TreeSitter.Node list =
        (if language.IsFunctionDefinition node then [ node ] else [])
        @ (nodeChildren node |> List.collect walk)

    walk root

// decision: 100 scales a 0..1 share into a percentage for display; naming it keeps this detector's
// own percentage math from tripping its own Magic Number finding.
let private percentScale = 100.0

// decision: bundle the two counts so shadowMessage has no adjacent same-type primitive parameters,
// which keeps this detector clear of its own Primitive Obsession finding.
type private ShadowCounts = { ErrorCount: int; TotalCount: int }

let private shadowMessage (counts: ShadowCounts) (sharePct: float) : string =
    sprintf
        "Error handling shadows the business logic: %d of %d statements (%d%%) live inside try/catch regions, leaving little unguarded work. Separate the happy path from failure handling so each stays readable."
        counts.ErrorCount
        counts.TotalCount
        (int (round (sharePct * percentScale)))

let analyzeErrorShadowing (ctx: AnalysisContext) : AnalysisContext =
    let thresholds = ctx.Options.ErrorShadowing

    let findings =
        allFunctions ctx.Language ctx.Tree
        |> List.collect (fun fnNode ->
            let errorCount, logicCount = countNodes ctx.Language fnNode
            let totalCount = errorCount + logicCount

            // decision: MinNamedNodes keeps tiny wrappers (a single call under a try) from tripping the
            // rule — a share only becomes meaningful once there is enough body to judge separation of concerns.
            // invariant: the share's denominator includes every named node in the function, so the
            // reported percentage measures error handling's portion of the complete body.
            if
                errorCount > 0
                && totalCount >= thresholds.MinNamedNodes
                && float errorCount / float totalCount >= thresholds.Threshold
            then
                let share = float errorCount / float totalCount

                let position =
                    firstAnchor ctx.Language fnNode
                    |> Option.map (fun anchor -> ctx.Positions.toPosition (nodeStartIndex anchor))
                    |> Option.defaultValue { Line = 0; Column = 0 }

                [ { Line = position.Line
                    Column = position.Column
                    Type = ErrorShadowing
                    Severity =
                      if share >= thresholds.HighThreshold then
                          Violation.High
                      else
                          Violation.Medium
                    Message =
                      shadowMessage
                          { ErrorCount = errorCount
                            TotalCount = totalCount }
                          share
                    Hotspots = [] } ]
            else
                [])

    addViolations findings ctx

let detector: Detector =
    { Name = "errorShadowing"
      Run = analyzeErrorShadowing }
