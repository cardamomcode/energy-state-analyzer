module Energy.Core.Detectors.ParameterCount

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

// Shared parameter-node lookup for the coherence pipeline's type-cohesion signal.
//
// decision: this is `findParametersNode` extracted from src/core/detectors/parameterCount.ts, ported
// early because typeCohesion.ts (a dependency of the coherence detector) needs it transitively.

// decision: searches direct children before descending — F#'s argument_patterns lives one level
// below function_declaration_left, while direct lookup keeps the common grammars cheap. This matches
// the existing detector and is shared with primitiveObsession's parameter-swap-risk check.
let rec findParametersNode (node: Node) (parametersType: string) : Node option =
    // first look for a direct child of the exact parameters type ...
    match nodeChildren node |> List.tryFind (fun c -> nodeType c = parametersType) with
    | Some direct -> Some direct
    // ... then fall back to a depth-first descent, returning the first match in any subtree.
    | None ->
        nodeChildren node
        |> List.collect (fun c -> findParametersNode c parametersType |> Option.toList)
        |> List.tryHead

// The "Parameter Explosion" detector. Flags a function after five parameters, escalating after
// eight; a violation is anchored at the function declaration rather than an arbitrary parameter.
let analyzeParameterCount (tree: Node) (positions: PositionLookup) (language: LanguageAdapter) : EnergyViolation list =
    let rec traverse (node: Node) : EnergyViolation list =
        let ownViolation =
            if language.IsFunctionDefinition node then
                match findParametersNode node language.NodeTypes.Parameters with
                | Some parameters ->
                    let parameterCount =
                        nodeChildren parameters
                        |> List.filter (fun child -> language.ParameterChildTypes |> List.contains (nodeType child))
                        |> List.length

                    // decision: flags past 5 parameters (medium) and 8 (high) — beyond roughly five,
                    // callers typically cannot recall argument order and meaning without the signature.
                    if parameterCount > 5 then
                        let position = positions.toPosition (nodeStartIndex node)

                        [ { Line = position.Line
                            Column = position.Column
                            Type = Parameters
                            Severity = if parameterCount > 8 then High else Medium
                            Message =
                              sprintf
                                  "Parameter explosion: %d parameters. Consider using objects or builder pattern."
                                  parameterCount
                            Hotspots = [] } ]
                    else
                        []
                | None -> []
            else
                []

        ownViolation @ (nodeChildren node |> List.collect traverse)

    traverse tree

let detector: Detector =
    { Name = "parameterCount"
      Run = fun ctx -> analyzeParameterCount ctx.Tree ctx.Positions ctx.Language }
