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
let private countNodes (language: LanguageAdapter.LanguageAdapter) (fnNode: TreeSitter.Node) : int * int =
    // decision: collect a per-named-node region flag first, then count — this keeps the metric
    // grammar-agnostic (the five grammars disagree on what counts as a statement node, but every one
    // yields named descendant nodes) and avoids a fold whose initial value could be mis-parsed.
    let rec walk (inErrorRegion: bool) (node: TreeSitter.Node) : bool list =
        if nodeIsNamed node then
            let nowInError =
                inErrorRegion || List.contains (nodeType node) language.ErrorHandlingAnchorTypes

            nowInError :: (nodeChildren node |> List.collect (walk nowInError))
        else
            // unnamed nodes are tokens/punctuation: descend so a named descendant inside an anchor is
            // still flagged, but never count the token itself.
            nodeChildren node |> List.collect (walk inErrorRegion)

    let flags = fnNode |> walk false
    let errorCount = flags |> List.filter id |> List.length
    let logicCount = flags |> List.filter (fun flag -> not flag) |> List.length
    (errorCount, logicCount)

// The first error-handling region inside a function — the anchor we point the violation at, so the
// reader lands on the try/catch doing the shadowing rather than the function's signature.
let private firstAnchor (language: LanguageAdapter.LanguageAdapter) (fnNode: TreeSitter.Node) : TreeSitter.Node option =
    let rec walk (node: TreeSitter.Node) : TreeSitter.Node option =
        if List.contains (nodeType node) language.ErrorHandlingAnchorTypes then
            Some node
        else
            nodeChildren node |> List.tryPick walk

    walk fnNode

// Every function definition anywhere in the tree, not just module-scope ones — methods nested inside a
// class are still functions whose logic can be shadowed by their own error handling.
let private allFunctions (language: LanguageAdapter.LanguageAdapter) (root: TreeSitter.Node) : TreeSitter.Node list =
    let rec walk (node: TreeSitter.Node) : TreeSitter.Node list =
        (if language.IsFunctionDefinition node then [ node ] else [])
        @ (nodeChildren node |> List.collect walk)

    walk root

let private shadowMessage (errorCount: int) (totalCount: int) (sharePct: float) : string =
    sprintf
        "Error handling shadows the business logic: %d of %d statements (%d%%) live inside try/catch regions, leaving little unguarded work. Separate the happy path from failure handling so each stays readable."
        errorCount
        totalCount
        (int (round (sharePct * 100.0)))

let analyzeErrorShadowing (ctx: AnalysisContext) : AnalysisContext =
    let thresholds = ctx.Options.ErrorShadowing

    let findings =
        allFunctions ctx.Language ctx.Tree
        |> List.collect (fun fnNode ->
            let errorCount, totalCount = countNodes ctx.Language fnNode

            // decision: MinNamedNodes keeps tiny wrappers (a single call under a try) from tripping the
            // rule — a share only becomes meaningful once there is enough body to judge separation of concerns.
            if
                totalCount >= thresholds.MinNamedNodes
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
                    Message = shadowMessage errorCount (errorCount + totalCount) share
                    Hotspots = [] } ]
            else
                [])

    addViolations findings ctx

let detector: Detector =
    { Name = "errorShadowing"
      Run = analyzeErrorShadowing }
