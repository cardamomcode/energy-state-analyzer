module Energy.Core.Detectors.ParameterCount


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

// Shared parameter-node lookup for the coherence pipeline's type-cohesion signal.
//
// Factor out the parameter-node lookup shared with the type-cohesion signal.
//
// decision: factors the parameter-node lookup out because the type-cohesion dependency needs it
// transitively.

/// Find a parameters node by checking direct children before descending into subtrees.
///
/// decision: searches direct children before descending — F#'s argument_patterns lives one level
/// below function_declaration_left, while direct lookup keeps the common grammars cheap. This matches
/// the existing detector and is shared with primitiveObsession's parameter-swap-risk check.
let rec findParametersNode (node: Node) (parametersType: NodeType) : Node option =
    // first look for a direct child of the exact parameters type ...
    match nodeChildren node |> List.tryFind (fun c -> nodeType c = parametersType) with
    | Some direct -> Some direct
    // ... then fall back to a depth-first descent, returning the first match in any subtree.
    | None ->
        nodeChildren node
        |> List.collect (fun c -> findParametersNode c parametersType |> Option.toList)
        |> List.tryHead

/// Score one grammar-normalized callable from its explicit parameter nodes.
///
/// decision: thresholds remain in Core.Config while adapters own parameter syntax, so project
/// overrides apply uniformly without forcing every callable form through one parameters node type.
let private analyzeCallable (ctx: AnalysisContext) (callable: CallableView) =
    let parameterCount = callable.Parameters.Length

    if parameterCount > ctx.Options.ParameterCount.MediumThreshold then
        let position = ctx.Positions.toPosition (nodeStartIndex callable.Anchor)

        [ { Line = position.Line
            Column = position.Column
            Type = Parameters
            Severity =
              if parameterCount > ctx.Options.ParameterCount.HighThreshold then
                  High
              else
                  Medium
            Message =
              sprintf "Parameter explosion: %d parameters. Consider using objects or builder pattern." parameterCount
            Hotspots = [] } ]
    else
        []

/// The "Parameter Explosion" detector. Flags a function past its medium threshold (5 by default),
/// escalating to high past the high threshold (8 by default); a violation is anchored at the function
/// declaration rather than an arbitrary parameter. Both thresholds are configurable — see Core.Config.
let analyzeParameterCount (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse (node: Node) : EnergyViolation list =
        // decision: analyzes the adapter's callable views so anonymous callables use their
        // grammar-specific parameter syntax and F# `and`-bindings remain split into logical heads.
        let ownViolations =
            ctx.Language.GetCallableViews node |> List.collect (analyzeCallable ctx)

        ownViolations @ (nodeChildren node |> List.collect traverse)

    let findings = traverse ctx.Tree
    addViolations findings ctx

let detector: Detector =
    { Name = "parameterCount"
      Run = analyzeParameterCount }
