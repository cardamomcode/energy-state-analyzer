module Energy.Core.Detectors.ErrorShadowing

open Energy.Core.Context
open Energy.Core.Config
open Energy.Core.Violation
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Begin boundary traversal at its owning function.
let private atFunctionRoot = true
/// Stop boundary traversal at nested function definitions.
let private descendIntoBody = false

/// Discover every function, including nested definitions, once.
let private allFunctions (language: LanguageAdapter) (root: Node) : Node list =
    let rec walk (node: Node) : Node list =
        (if language.IsFunctionDefinition node then [ node ] else [])
        @ (nodeChildren node |> List.collect walk)

    walk root

/// Collect boundaries belonging to this function without visiting nested functions.
let private regionsInFunction (language: LanguageAdapter) (fnNode: Node) : ErrorHandlingRegion list =
    let rec walk (isFunctionRoot: bool) (node: Node) : ErrorHandlingRegion list =
        if not isFunctionRoot && language.IsFunctionDefinition node then
            []
        else
            (language.GetErrorHandlingRegion node |> Option.toList)
            @ (nodeChildren node |> List.collect (walk descendIntoBody))

    walk atFunctionRoot fnNode

/// Expand a logical item into its constituent items, recursing into a nested try's protected and
/// recovery items rather than counting the try itself as one item.
let rec private expand (language: LanguageAdapter) (item: Node) : Node list =
    match language.GetErrorHandlingRegion item with
    | Some region -> (region.ProtectedItems @ region.RecoveryItems) |> List.collect (expand language)
    | None -> [ item ]

/// Count a function's logical items, expanding a try into its protected and recovery items for the denominator.
///
/// decision: a try is expanded into its direct protected and recovery items for the function denominator,
/// while other compound statements remain one item. This measures exception-boundary breadth without AST
/// scaffolding (identifiers/calls/arguments) or nested control-flow internals changing the result.
let private functionItems (language: LanguageAdapter) (fnNode: Node) : Node list =
    language.GetFunctionLogicalItems fnNode |> List.collect (expand language)

/// Convert a logical-work share to its displayed percentage.
let private percentScale = 100.0

/// Describe one qualifying logical-work share and its severity.
type private ModeMeasurement =
    { ItemCount: int
      Share: float
      Severity: Severity }

/// Pair boundary work with its enclosing function denominator.
type private MeasurementInput =
    { ItemCount: int
      FunctionItemCount: int }

/// Apply the configured minimum size and severity shares.
let private qualifies (thresholds: ErrorShadowingModeThresholds) (input: MeasurementInput) : ModeMeasurement option =
    if input.ItemCount < thresholds.MinItems || input.FunctionItemCount = 0 then
        None
    else
        let share = float input.ItemCount / float input.FunctionItemCount

        if share < thresholds.Threshold then
            None
        else
            Some
                { ItemCount = input.ItemCount
                  Share = share
                  Severity = if share >= thresholds.HighThreshold then High else Medium }

/// Construct a finding at the boundary or individual recovery clause.
let private finding (ctx: AnalysisContext) anchor kind severity message =
    let position = ctx.Positions.toPosition (nodeStartIndex anchor)

    { Line = position.Line
      Column = position.Column
      Type = kind
      Severity = severity
      Message = message
      Hotspots = [] }

/// Pair a rule's identity and thresholds with the body work it measures.
type private ShareRule =
    { Kind: ViolationType
      Label: string
      Advice: string
      Thresholds: ErrorShadowingModeThresholds
      Items: Node list }

/// Evaluate one logical-share rule without combining independent diagnostics.
let private shareFinding ctx (region: ErrorHandlingRegion) totalItems rule =
    qualifies
        rule.Thresholds
        { ItemCount = List.length rule.Items
          FunctionItemCount = totalItems }
    |> Option.map (fun measurement ->
        sprintf
            "%s: %d logical items (%d%%) of %d function logical items. %s"
            rule.Label
            measurement.ItemCount
            (int (round (measurement.Share * percentScale)))
            totalItems
            rule.Advice
        |> finding ctx region.Anchor rule.Kind measurement.Severity)
    |> Option.toList

/// Evaluate each recovery body independently of protected scope and recovery share.
let private oversizedFindings ctx (region: ErrorHandlingRegion) =
    let thresholds = ctx.Options.ErrorShadowing

    if not thresholds.OversizedRecoveryBlockEnabled then
        []
    else
        region.RecoveryBodies
        |> List.choose (fun body ->
            let lines = Energy.Core.BodyLines.count ctx.Source ctx.Positions body.Items

            if lines <= thresholds.RecoveryBlock.MaxLines then
                None
            else
                sprintf
                    "Oversized recovery block: %d body lines exceeds the maximum of %d. Extract or simplify this handler or cleanup body."
                    lines
                    thresholds.RecoveryBlock.MaxLines
                |> finding ctx body.Anchor OversizedRecoveryBlock Medium
                |> Some)

/// Evaluate the two share rules and the independent body-size rule at one boundary.
let private boundaryFindings ctx totalItems region =
    let thresholds = ctx.Options.ErrorShadowing

    [ yield!
          shareFinding
              ctx
              region
              totalItems
              { Kind = ErrorShadowing
                Label = "Broad protected scope"
                Advice = "Narrow the protected region to operations this boundary can recover from."
                Thresholds = thresholds.ProtectedScope
                Items = region.ProtectedItems }
      if
          thresholds.RecoveryDominanceEnabled
          && Energy.Core.BodyLines.count ctx.Source ctx.Positions region.ProtectedBody > 1
      then
          yield!
              shareFinding
                  ctx
                  region
                  totalItems
                  { Kind = RecoveryDominance
                    Label = "Recovery/cleanup dominance"
                    Advice = "Extract or simplify recovery and cleanup policy so the happy path remains clear."
                    Thresholds = thresholds.Recovery
                    Items = region.RecoveryItems }
      yield! oversizedFindings ctx region ]

/// Evaluate broad protected scope, recovery dominance, and each oversized recovery body independently.
let analyzeErrorShadowing (ctx: AnalysisContext) : AnalysisContext =
    allFunctions ctx.Language ctx.Tree
    |> List.collect (fun fnNode ->
        let totalItems = functionItems ctx.Language fnNode |> List.length

        regionsInFunction ctx.Language fnNode
        |> List.collect (boundaryFindings ctx totalItems))
    |> fun findings -> addViolations findings ctx

/// Register the error-boundary family behind its existing host switch.
let detector: Detector =
    { Name = "errorShadowing"
      Run = analyzeErrorShadowing }
