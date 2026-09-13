module Energy.Languages.CognitiveSyntax

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Recognize conditional forms shared by the supported grammars.
let private isConditional node =
    List.contains (nodeType node) [ NodeType "if_statement"; NodeType "if_expression" ]

/// Recognize an explicit elif branch without treating it as a nested if.
let private isElif node =
    List.contains (nodeType node) [ NodeType "elif_clause"; NodeType "elif_expression" ]

/// Ignore comments when locating the body following an else keyword.
let private significantChildren node =
    nodeChildren node
    |> List.filter (fun child ->
        not (List.contains (nodeType child) [ NodeType "comment"; NodeType "line_comment"; NodeType "block_comment" ]))

/// Find an else keyword's following branch in grammars without an else-clause wrapper.
let private afterElse node =
    significantChildren node
    |> List.skipWhile (fun child -> nodeType child <> NodeType "else")
    |> List.tryItem 1

/// Identify else-if by its position, never by whether an else block contains only one statement.
///
/// invariant: adding braces around an if in an else body creates genuine nesting, not an else-if.
let private isElseIf node =
    nodeParent node
    |> Option.exists (fun parent ->
        nodeType parent = NodeType "else_clause"
        || (isConditional parent
            && (afterElse parent |> Option.exists (fun next -> nodeId next = nodeId node))))

/// Select control-flow bodies independently of their block or single-statement spelling.
let private nestedChildren node =
    let named = nodeNamedChildren node

    let fields =
        [ "body"; "consequence"; "then"; "else"; "alternative"; "block" ]
        |> List.choose (fun field -> nodeField field node)

    let bodies =
        if not fields.IsEmpty then
            fields
        elif isConditional node || isElif node then
            let condition =
                nodeField "condition" node
                |> Option.orElseWith (fun () -> nodeField "guard" node)

            named
            |> List.filter (fun child -> condition |> Option.forall (fun guard -> nodeId guard <> nodeId child))
        elif nodeType node = NodeType "when_expression" then
            named |> List.filter (fun child -> nodeType child = NodeType "when_entry")
        elif nodeType node = NodeType "do_while_statement" then
            named |> List.truncate 1
        else
            named |> List.rev |> List.truncate 1

    bodies
    |> List.filter (fun child ->
        nodeType child <> NodeType "else_clause"
        && not (isElif child)
        && not (isConditional child && isElseIf child))

/// Distinguish F# exception rules from ordinary match arms.
let private isCatchRule node =
    nodeType node = NodeType "rule"
    && (nodeParent node
        |> Option.bind nodeParent
        |> Option.exists (fun parent -> nodeType parent = NodeType "try_expression"))

/// Score only unconditional goto or explicitly labelled break/continue jumps.
let private isLabelJump node =
    match nodeType node with
    | NodeType "goto_statement" -> true
    | NodeType "break_statement"
    | NodeType "continue_statement" -> not (nodeNamedChildren node).IsEmpty
    | NodeType "labeled_expression" ->
        // decision: this Kotlin grammar parses break@target as a label plus identifier;
        // return@target remains a return_expression and receives no jump increment.
        nodeNamedChildren node
        |> List.tryHead
        |> Option.exists (fun label -> nodeText label = "break@" || nodeText label = "continue@")
    | _ -> false

/// Normalize syntax into the paper's increments and body nesting; adapters supply decision types.
///
/// decision: normalize else-if wrappers and body fields here so the shared detector has no grammar
/// names and optional braces cannot change a score.
let classify decisions node : CognitiveStructure option =
    match nodeType node with
    | NodeType "parenthesized_expression"
    | NodeType "paren_expression" -> nodeNamedChildren node |> List.tryExactlyOne |> Option.map BooleanGroup
    | NodeType "arrow_function"
    | NodeType "lambda"
    | NodeType "lambda_expression"
    | NodeType "lambda_literal"
    | NodeType "fun_expression"
    | NodeType "function_expression"
    | NodeType "anonymous_method_expression" -> Some Closure
    | NodeType "else_clause" ->
        let isTryElse =
            nodeParent node
            |> Option.exists (fun parent -> nodeType parent = NodeType "try_statement")

        if isTryElse || (afterElse node |> Option.exists isConditional) then
            None
        else
            Some(Flow(Hybrid, nodeNamedChildren node))
    | NodeType "else" ->
        match nodeParent node with
        | Some parent when isConditional parent && not (afterElse parent |> Option.exists isConditional) ->
            Some(Flow(Fundamental, []))
        | _ -> None
    | _ when isElif node || (isConditional node && isElseIf node) -> Some(Flow(Hybrid, nestedChildren node))
    | _ when List.contains (nodeType node) decisions || isCatchRule node -> Some(Flow(Structural, nestedChildren node))
    | NodeType "ternary_expression"
    | NodeType "conditional_expression" ->
        let branches =
            [ "consequence"; "alternative" ]
            |> List.choose (fun field -> nodeField field node)

        let bodies =
            if branches.IsEmpty then
                let named = nodeNamedChildren node
                [ List.tryHead named; List.tryLast named ] |> List.choose id
            else
                branches

        Some(Flow(Structural, bodies))
    | _ when isLabelJump node -> Some(Flow(Fundamental, []))
    | _ -> None
