module Energy.Core.Detectors.ErrorShadowing

open Energy.Core
open Energy.Core.Context
open Energy.Core.Config
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// ESA-013 measures two distinct error-boundary smells at each try construct: protected scope that
// catches too much of a function, and recovery/cleanup that dominates it. Both are prompts to review
// the boundary, not proof that a try block is inherently wrong.

let private atFunctionRoot = true
let private descendIntoBody = false

let private allFunctions (language: LanguageAdapter) (root: Node) : Node list =
    let rec walk (node: Node) : Node list =
        (if language.IsFunctionDefinition node then [ node ] else [])
        @ (nodeChildren node |> List.collect walk)

    walk root

let private regionsInFunction (language: LanguageAdapter) (fnNode: Node) : ErrorHandlingRegion list =
    let rec walk (isFunctionRoot: bool) (node: Node) : ErrorHandlingRegion list =
        if not isFunctionRoot && language.IsFunctionDefinition node then
            []
        else
            (language.GetErrorHandlingRegion node |> Option.toList)
            @ (nodeChildren node |> List.collect (walk descendIntoBody))

    walk atFunctionRoot fnNode

// decision: a try is expanded into its direct protected and recovery items for the function denominator,
// while other compound statements remain one item. This measures exception-boundary breadth without AST
// scaffolding (identifiers/calls/arguments) or nested control-flow internals changing the result.
let private functionItems (language: LanguageAdapter) (fnNode: Node) : Node list =
    let rec expand (item: Node) : Node list =
        match language.GetErrorHandlingRegion item with
        | Some region -> (region.ProtectedItems @ region.RecoveryItems) |> List.collect expand
        | None -> [ item ]

    language.GetFunctionLogicalItems fnNode |> List.collect expand

let private percentScale = 100.0

type private ModeMeasurement =
    { ItemCount: int
      Share: float
      Severity: Severity }

type private BoundaryMeasurements =
    { ProtectedScope: ModeMeasurement option
      Recovery: ModeMeasurement option }

type private MeasurementInput =
    { ItemCount: int
      FunctionItemCount: int }

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

let private combinedSeverity (measurements: BoundaryMeasurements) : Severity =
    [ measurements.ProtectedScope; measurements.Recovery ]
    |> List.choose id
    |> List.map _.Severity
    |> List.max

let private describeMeasurement (label: string) (measurement: ModeMeasurement) =
    sprintf "%s: %d logical items (%d%%)" label measurement.ItemCount (int (round (measurement.Share * percentScale)))

let private shadowMessage (measurements: BoundaryMeasurements) (totalItems: int) : string =
    let facts =
        [ measurements.ProtectedScope
          |> Option.map (describeMeasurement "protected scope")
          measurements.Recovery |> Option.map (describeMeasurement "recovery/cleanup") ]
        |> List.choose id
        |> String.concat "; "

    let remediation =
        match measurements.ProtectedScope, measurements.Recovery with
        | Some _, Some _ -> "Narrow the protected region and extract or simplify failure handling."
        | Some _, None ->
            "Narrow the protected region so it catches only the operations this boundary can recover from."
        | None, Some _ -> "Extract or simplify recovery and cleanup policy so the happy path remains clear."
        | None, None -> ""

    sprintf "Error boundary scope: %s of %d function logical items. %s" facts totalItems remediation

let analyzeErrorShadowing (ctx: AnalysisContext) : AnalysisContext =
    let thresholds = ctx.Options.ErrorShadowing

    let findings =
        allFunctions ctx.Language ctx.Tree
        |> List.collect (fun fnNode ->
            let totalItems = functionItems ctx.Language fnNode |> List.length

            regionsInFunction ctx.Language fnNode
            |> List.choose (fun region ->
                let measurements =
                    { ProtectedScope =
                        qualifies
                            thresholds.ProtectedScope
                            { ItemCount = region.ProtectedItems.Length
                              FunctionItemCount = totalItems }
                      Recovery =
                        qualifies
                            thresholds.Recovery
                            { ItemCount = region.RecoveryItems.Length
                              FunctionItemCount = totalItems } }

                if measurements.ProtectedScope.IsNone && measurements.Recovery.IsNone then
                    None
                else
                    let position = ctx.Positions.toPosition (nodeStartIndex region.Anchor)

                    Some
                        { Line = position.Line
                          Column = position.Column
                          Type = ErrorShadowing
                          Severity = combinedSeverity measurements
                          Message = shadowMessage measurements totalItems
                          Hotspots = [] }))

    addViolations findings ctx

let detector: Detector =
    { Name = "errorShadowing"
      Run = analyzeErrorShadowing }
