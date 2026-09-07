module Energy.Core.Detectors.ParameterCount


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

// Shared parameter-node lookup for the coherence pipeline's type-cohesion signal.
//
// decision: factors the parameter-node lookup out because the type-cohesion dependency needs it
// transitively.

// decision: searches direct children before descending — F#'s argument_patterns lives one level
// below function_declaration_left, while direct lookup keeps the common grammars cheap. This matches
// the existing detector and is shared with primitiveObsession's parameter-swap-risk check.
let rec findParametersNode (node: Node) (parametersType: NodeType) : Node option =
    // first look for a direct child of the exact parameters type ...
    match nodeChildren node |> List.tryFind (fun c -> nodeType c = parametersType) with
    | Some direct -> Some direct
    // ... then fall back to a depth-first descent, returning the first match in any subtree.
    | None ->
        nodeChildren node
        |> List.collect (fun c -> findParametersNode c parametersType |> Option.toList)
        |> List.tryHead

// The "Parameter Explosion" detector. Flags a function past its medium threshold (5 by default),
// escalating to high past the high threshold (8 by default); a violation is anchored at the function
// declaration rather than an arbitrary parameter. Both thresholds are configurable — see Core.Config.
let analyzeParameterCount (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse (node: Node) : EnergyViolation list =
        // decision: analyzes each logical head of a definition (F#'s `and`-binding splits into one head
        // per mutually recursive let) rather than the merged node, so every head's parameter count is
        // measured instead of only the first head's. For a single-head definition this is one head, so
        // behavior is unchanged.
        let ownViolations =
            if ctx.Language.IsFunctionDefinition node then
                ctx.Language.GetFunctionHeads node
                |> List.collect (fun head ->
                    match findParametersNode head.ParametersRoot ctx.Language.NodeTypes.Parameters with
                    | Some parameters ->
                        let parameterCount =
                            nodeChildren parameters
                            |> List.filter (fun child ->
                                ctx.Language.ParameterChildTypes |> List.contains (nodeType child))
                            |> List.length

                        // decision: thresholds live in Core.Config as the single source of truth; this detector
                        // reads them from ctx.Options so a project (.esaconfig.json) or host (VS Code/CLI) can
                        // retune without editing code — past medium is medium energy, past high escalates to high.
                        let mediumThreshold = ctx.Options.ParameterCount.MediumThreshold
                        let highThreshold = ctx.Options.ParameterCount.HighThreshold

                        if parameterCount > mediumThreshold then
                            // Anchor at the head (the function name), not the merged defn, so an `and`-bound
                            // function's finding lands on its own declaration.
                            let position = ctx.Positions.toPosition (nodeStartIndex head.ParametersRoot)

                            [ { Line = position.Line
                                Column = position.Column
                                Type = Parameters
                                Severity = if parameterCount > highThreshold then High else Medium
                                Message =
                                  sprintf
                                      "Parameter explosion: %d parameters. Consider using objects or builder pattern."
                                      parameterCount
                                Hotspots = [] } ]
                        else
                            []
                    | None -> [])
            else
                []

        ownViolations @ (nodeChildren node |> List.collect traverse)

    let findings = traverse ctx.Tree
    addViolations findings ctx

let detector: Detector =
    { Name = "parameterCount"
      Run = analyzeParameterCount }
