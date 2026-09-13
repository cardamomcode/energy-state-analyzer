module Energy.Languages.ValidationSyntax

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Grammar shapes needed to recognize a straight-line guard with an unchanged or absent success value.
type GuardSyntax =
    { Containers: NodeType list
      Conditional: NodeType
      Rejections: NodeType list
      Return: NodeType option
      FailureCalls: string list
      EmptyValues: string list
      PreservesCheckedInformation: Node -> bool }

/// Discard comments while retaining every executable statement.
let private children node =
    nodeNamedChildren node
    |> List.filter (fun child ->
        not (List.contains (nodeType child) [ NodeType "comment"; NodeType "line_comment"; NodeType "block_comment" ]))

/// Unwrap only grammar containers, never loops, handlers, or nested declarations.
let rec private statements syntax node =
    if List.contains (nodeType node) syntax.Containers then
        children node |> List.collect (statements syntax)
    else
        [ node ]

/// Recognize F# failure applications by their leftmost callee, not text inside arguments.
let rec private callee node =
    if nodeType node = NodeType "application_expression" then
        children node |> List.tryHead |> Option.bind callee
    else
        Some(nodeText node)

/// Establish that the entire branch rejects; a nested conditional throw is insufficient.
let private rejects syntax node =
    match statements syntax node with
    | [ statement ] ->
        List.contains (nodeType statement) syntax.Rejections
        || (nodeType statement = NodeType "application_expression"
            && (callee statement
                |> Option.exists (fun name -> List.contains name syntax.FailureCalls)))
    | _ -> false

/// Decode explicit returns separately from implicit fallthrough.
let private success syntax returned =
    let value =
        match syntax.Return with
        | Some kind when nodeType returned = kind ->
            match children returned with
            | [] -> Some NoValue
            | [ value ] ->
                if List.contains (nodeText value) syntax.EmptyValues then
                    Some NoValue
                else
                    Some(UnchangedInput value)
            | _ -> None
        | None ->
            if List.contains (nodeText returned) syntax.EmptyValues then
                Some NoValue
            else
                Some(UnchangedInput returned)
        | _ -> None

    value

/// Read only the function's body, including F#'s single-expression body.
let private bodyItems syntax (head: FunctionHead) =
    if
        List.contains (nodeType head.Body) syntax.Containers
        || nodeType head.Body = syntax.Conditional
    then
        statements syntax head.Body
    else
        children head.Body
        |> List.filter (fun node ->
            List.contains (nodeType node) syntax.Containers
            || nodeType node = syntax.Conditional)
        |> List.collect (statements syntax)

/// Extract a rejecting guard with either an identity return or no success value.
///
/// decision: accepts only a guard and optional return so mutation, recovery, and intervening work cannot masquerade as a check-only validator.
let extract syntax (head: FunctionHead) : GuardedValidation option =
    let candidate =
        match
            (if syntax.PreservesCheckedInformation head.ParametersRoot then
                 []
             else
                 bodyItems syntax head)
        with
        | [ guard ] -> Some(guard, NoValue)
        | [ guard; returned ] -> success syntax returned |> Option.map (fun result -> guard, result)
        | _ -> None

    candidate
    |> Option.bind (fun (guard, result) ->
        if nodeType guard <> syntax.Conditional then
            None
        else
            match children guard with
            | [ condition; rejection ] when rejects syntax rejection ->
                Some
                    { Anchor = guard
                      Condition = condition
                      Success = result }
            | _ -> None)
