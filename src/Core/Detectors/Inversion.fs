module Energy.Core.Detectors.Inversion


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

let private hasType expected node =
    expected |> Option.exists ((=) (nodeType node))

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

let private functionStatements (language: LanguageAdapter) (body: Node) =
    nodeChildren body
    |> List.filter (fun child ->
        nodeIsNamed child
        && not (hasType language.NodeTypes.Comment child)
        && not (System.String.IsNullOrWhiteSpace(nodeText child)))

let private hasElse (language: LanguageAdapter) (ifNode: Node) =
    match language.NodeTypes.ElseClause with
    | Some _ -> nodeChildren ifNode |> List.exists (hasType language.NodeTypes.ElseClause)
    | None ->
        let blocks = nodeChildren ifNode |> List.filter (hasType language.NodeTypes.Block)

        blocks.Length > 1
        || (nodeChildren ifNode |> List.exists (hasType language.NodeTypes.IfStatement))

let private nestedValidation (language: LanguageAdapter) (body: Node) =
    let rec collect current level checks =
        if level >= 4 then
            checks
        else
            let statements =
                nodeChildren current
                |> List.filter (fun child -> List.contains (nodeType child) language.NestingControlTypes)

            match statements with
            | [ ifNode ] when hasType language.NodeTypes.IfStatement ifNode && not (hasElse language ifNode) ->
                match nodeChildren ifNode |> List.tryFind (hasType language.NodeTypes.Block) with
                | Some ifBody -> collect ifBody (level + 1) (ifNode :: checks)
                | None -> ifNode :: checks
            | _ -> checks

    collect body 0 [] |> List.rev

let private deepestIf (language: LanguageAdapter) (body: Node) =
    let rec walk node depth =
        if language.IsFunctionDefinition node then
            0, None
        else
            let ownDepth, ownNode =
                if hasType language.NodeTypes.IfStatement node then
                    depth, Some node
                else
                    0, None

            let childDepth = if ownNode.IsSome then depth + 1 else depth

            nodeChildren node
            |> List.fold
                (fun (bestDepth, bestNode) child ->
                    let candidateDepth, candidateNode = walk child childDepth

                    if candidateDepth > bestDepth then
                        candidateDepth, candidateNode
                    else
                        bestDepth, bestNode)
                (ownDepth, ownNode)

    nodeChildren body
    |> List.fold
        (fun (bestDepth, bestNode) child ->
            let depth, location = walk child 0

            if depth > bestDepth then
                depth, location
            else
                bestDepth, bestNode)
        (0, None)

let private analyzeFunction (positions: PositionLookup) (language: LanguageAdapter) (functionNode: Node) =
    match findBody language functionNode with
    | None -> []
    | Some body ->
        let dominant =
            match functionStatements language body with
            | first :: _ when hasType language.NodeTypes.IfStatement first ->
                match nodeChildren first |> List.tryFind (hasType language.NodeTypes.Block) with
                | Some ifBody when (nodeChildren ifBody).Length > 2 ->
                    let ratio =
                        float (nodeEndIndex ifBody - nodeStartIndex ifBody)
                        / float (nodeEndIndex functionNode - nodeStartIndex functionNode)

                    if ratio > 0.5 then
                        let position = positions.toPosition (nodeStartIndex first)

                        [ { Line = position.Line
                            Column = position.Column
                            Type = Inversion
                            Severity = Medium
                            Message = "Consider inverting this condition and using early return for cleaner flow."
                            Hotspots = [] } ]
                    else
                        []
                | _ -> []
            | _ -> []

        let validations = nestedValidation language body

        let validationFinding =
            match validations with
            | first :: _ when validations.Length >= 2 ->
                let position = positions.toPosition (nodeStartIndex first)

                [ { Line = position.Line
                    Column = position.Column
                    Type = Inversion
                    Severity = Medium
                    Message =
                      sprintf
                          "Found %d nested validation checks. Consider using guard clauses with early returns."
                          validations.Length
                    Hotspots = [] } ]
            | _ -> []

        let depth, location = deepestIf language body

        let deepFinding =
            match location with
            | Some node when depth >= 3 ->
                let position = positions.toPosition (nodeStartIndex node)

                [ { Line = position.Line
                    Column = position.Column
                    Type = Inversion
                    Severity = Medium
                    Message =
                      sprintf
                          "Deep if-nesting (%d levels). Consider inverting conditions or extracting functions."
                          depth
                    Hotspots = [] } ]
            | _ -> []

        dominant @ validationFinding @ deepFinding

let analyzeInversionOpportunities (tree: Node) (positions: PositionLookup) (language: LanguageAdapter) =
    let rec traverse node =
        let own =
            if language.IsFunctionDefinition node then
                analyzeFunction positions language node
            else
                []

        own @ (nodeChildren node |> List.collect traverse)

    traverse tree

let detector: Detector =
    { Name = "inversion"
      Run = fun ctx -> analyzeInversionOpportunities ctx.Tree ctx.Positions ctx.Language }

let handler: Energy.Core.AnalysisPipeline.AnalysisHandler =
    Energy.Core.AnalysisPipeline.detector detector.Run
