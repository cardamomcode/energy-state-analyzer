module Energy.Core.Detectors.ParameterCount

open Energy.Core.TreeSitter

// Shared parameter-node lookup for the coherence pipeline's type-cohesion signal.
//
// decision: this is `findParametersNode` extracted from src/core/detectors/parameterCount.ts, ported
// early because typeCohesion.ts (a dependency of the coherence detector) needs it transitively. The
// full parameter-count DETECTOR and its own integration suite are a separate, later batch per the
// rewrite plan (§4), so this file deliberately holds only the shared helper — no `detector` value,
// no registration in Analyze.fs's allDetectors yet. When that batch lands it owns this module.

// decision: does a level-by-level (breadth-first) search rather than a depth-first one — finds a
// function's own parameters node even when nested a level below the function node itself (e.g. F#'s
// argument_patterns sits inside function_declaration_left), while still stopping before it can reach a
// nested function's parameters. Shared with primitiveObsession.ts's parameter-swap-risk check — same
// structural problem, same fix.
let rec findParametersNode (node: Node) (parametersType: string) : Node option =
    // first look for a direct child of the exact parameters type ...
    match nodeChildren node |> List.tryFind (fun c -> nodeType c = parametersType) with
    | Some direct -> Some direct
    // ... then fall back to a depth-first descent, returning the first match in any subtree.
    | None ->
        nodeChildren node
        |> List.collect (fun c -> findParametersNode c parametersType |> Option.toList)
        |> List.tryHead
