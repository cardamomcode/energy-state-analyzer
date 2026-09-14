module Energy.Core.Detectors.Inversion

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

/// Named constants configuring the inversion-detection heuristics.
///
/// decision: these inversion-detection thresholds are detector heuristics, not published or
/// user-tunable metric values, so they stay as named constants at the top of the module rather
/// than in Core.Config, keeping the rationale visible next to the module's other declarations.
let private inversionRatioThreshold = 0.5

/// Minimum number of conditional levels that warrants extraction advice.
let private deepIfDepthThreshold = 3

/// Match an optional grammar node type.
let private hasType expected node =
    expected |> Option.exists ((=) (nodeType node))

/// Locate a function's explicit block, including Kotlin's function-body wrapper.
let private findBody (language: LanguageAdapter) (functionNode: Node) =
    let direct =
        nodeChildren functionNode |> List.tryFind (hasType language.NodeTypes.Block)

    match direct with
    | Some body -> Some body
    // decision: Kotlin puts a function block below function_body, unlike Python and TypeScript.
    | None ->
        nodeChildren functionNode
        |> List.collect nodeChildren
        |> List.tryFind (hasType language.NodeTypes.Block)

/// Retain all executable statements, ignoring punctuation and both comment spellings.
let private functionStatements (language: LanguageAdapter) (body: Node) =
    nodeNamedChildren body
    |> List.filter (fun child ->
        not (hasType language.NodeTypes.Comment child)
        && not (List.contains (nodeType child) [ NodeType "line_comment"; NodeType "block_comment" ])
        && not (System.String.IsNullOrWhiteSpace(nodeText child)))

/// Recognize alternatives even when the grammar represents else as a bare keyword.
let private hasElse (language: LanguageAdapter) (ifNode: Node) =
    not (language.GetElseIfBranches ifNode).IsEmpty
    || (nodeChildren ifNode
        |> List.exists (fun child -> hasType language.NodeTypes.ElseClause child || nodeType child = NodeType "else"))

/// Select a terminal conditional whose failure reaches only a return or the function end.
///
/// decision: guard advice requires a terminal function-level conditional; ordinary following work
/// and statements between guard levels are never discarded to manufacture a validation chain.
let private terminalConditional language body =
    match functionStatements language body with
    | [ node ] when hasType language.NodeTypes.IfStatement node -> Some node
    | [ node; fallback ] when
        hasType language.NodeTypes.IfStatement node
        && List.contains (nodeType fallback) [ NodeType "return_statement"; NodeType "return_expression" ]
        ->
        Some node
    | _ -> None

/// Count consecutive else-free conditionals without crossing intervening work or control flow.
let rec private guardChain language node =
    if hasElse language node then
        []
    else
        let nested =
            nodeChildren node
            |> List.tryFind (hasType language.NodeTypes.Block)
            |> Option.map (functionStatements language)
            |> Option.defaultValue []

        match nested with
        | [ child ] when hasType language.NodeTypes.IfStatement child -> node :: guardChain language child
        | _ -> [ node ]

/// Keep the first deepest location when several branches have the same depth.
let private deeper first second =
    if fst second > fst first then second else first

/// Find conditional depth using the adapters' normalized branch bodies.
///
/// invariant: alternatives stay at the same conditional depth; only their bodies add a level.
/// decision: share cognitive syntax normalization so braces and elif spellings agree across rules.
let rec private deepestIfNode (language: LanguageAdapter) node depth =
    let structure = language.GetCognitiveStructure node

    if language.IsFunctionDefinition node || structure = Some Closure then
        0, None
    else
        let conditional =
            hasType language.NodeTypes.IfStatement node
            || (nodeParent node
                |> Option.exists (fun parent ->
                    language.GetElseIfBranches parent
                    |> List.exists (fun branch -> nodeId branch = nodeId node)))

        let own = if conditional then depth + 1, Some node else 0, None

        let bodies =
            match structure with
            | Some(Flow(Hybrid, nested)) -> nested
            | Some(Flow(_, nested)) when conditional -> nested
            | _ -> []

        nodeChildren node
        |> List.map (fun child ->
            let nested = bodies |> List.exists (fun body -> nodeId body = nodeId child)
            deepestIfNode language child (if nested then depth + 1 else depth))
        |> List.fold deeper own

/// Detect a large terminal conditional using executable statement count and source span.
let private dominantBody language functionNode node =
    nodeChildren node
    |> List.tryFind (hasType language.NodeTypes.Block)
    |> Option.exists (fun body ->
        let ratio =
            float (nodeEndIndex body - nodeStartIndex body)
            / float (nodeEndIndex functionNode - nodeStartIndex functionNode)

        (functionStatements language body).Length > 2 && ratio > inversionRatioThreshold)

/// Prefer specific guard advice over a general depth finding for the same function.
///
/// decision: emit at most one inversion finding per function so overlapping heuristics do not
/// multiply its score or repeat competing advice in Problems.
let private recommendation language functionNode body =
    let guards =
        terminalConditional language body
        |> Option.map (guardChain language)
        |> Option.defaultValue []

    match guards with
    | first :: _ when guards.Length >= 2 ->
        Some(
            first,
            sprintf
                "These %d nested conditions keep the main operation indented. Consider guard clauses, preserving the existing return values and fallthrough behavior."
                guards.Length
        )
    | [ first ] when dominantBody language functionNode first ->
        Some(
            first,
            "This condition encloses most of the function. Consider a guard clause to bring the main operation to the top level, preserving the existing return values and fallthrough behavior."
        )
    | _ ->
        let depth, location = deepestIfNode language body 0

        location
        |> Option.filter (fun _ -> depth >= deepIfDepthThreshold)
        |> Option.map (fun node ->
            node,
            sprintf
                "These conditions are nested %d levels deep. Consider extracting a named operation to reduce how many conditions readers must track."
                depth)

/// Analyze block-based functions and anchor one actionable recommendation to each finding.
let analyzeInversionOpportunities (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse node =
        let own =
            if ctx.Language.IsFunctionDefinition node then
                findBody ctx.Language node
                |> Option.bind (recommendation ctx.Language node)
                |> Option.map (fun (anchor, message) ->
                    let position = ctx.Positions.toPosition (nodeStartIndex anchor)

                    { Line = position.Line
                      Column = position.Column
                      Type = Inversion
                      Severity = Medium
                      Message = message
                      Hotspots = [] })
                |> Option.toList
            else
                []

        own @ (nodeChildren node |> List.collect traverse)

    addViolations (traverse ctx.Tree) ctx

/// Register the shared inversion detector.
let detector: Detector =
    { Name = "inversion"
      Run = analyzeInversionOpportunities }
