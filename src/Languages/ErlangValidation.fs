module Energy.Languages.ErlangValidation

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Name the atom used by an unconditional Erlang if clause.
let private trueAtom = "true"

/// Read a clause only when its body has one executable expression.
let private singleClauseExpression clause =
    nodeField "body" clause
    |> Option.bind (fun body ->
        match nodeNamedChildren body with
        | [ expression ] -> Some expression
        | _ -> None)

/// Recognize Erlang's terminating BIFs without treating arbitrary calls as rejection.
let private isFailureCall node =
    let call =
        match nodeType node with
        | NodeType "call" -> Some node
        | NodeType "remote" when nodeField "module" node |> Option.exists (fun part -> nodeText part = "erlang:") ->
            nodeField "fun" node
        | _ -> None

    call
    |> Option.exists (fun expression ->
        let name = nodeField "expr" expression |> Option.map nodeText

        let arity =
            nodeField "args" expression |> Option.map (nodeNamedChildren >> List.length)

        match name, arity with
        | Some "error", Some(1 | 2)
        | Some "throw", Some 1
        | Some "exit", Some 1 -> true
        | _ -> false)

/// Classify a direct identity or literal-boolean success result.
let private validationSuccess node =
    match nodeType node with
    | NodeType "var" -> Some(UnchangedInput node)
    | NodeType "atom" when List.contains (nodeText node) [ trueAtom; "false" ] -> Some(BareBoolean node)
    | _ -> None

/// Read a rejecting case clause followed by one success clause.
let private caseValidation decision =
    let clauses =
        nodeNamedChildren decision
        |> List.filter (fun child -> nodeType child = NodeType "cr_clause")

    match nodeField "expr" decision, clauses with
    | Some condition, [ rejection; success ] when
        (nodeField "pat" rejection
         |> Option.exists (fun pattern ->
             List.contains
                 (nodeType pattern)
                 [
                     NodeType "atom"
                     NodeType "integer"
                     NodeType "float"
                     NodeType "string"
                     NodeType "list"
                     NodeType "tuple"
                 ]))
        && nodeField "guard" rejection |> Option.isNone
        && nodeField "guard" success |> Option.isNone
        ->
        match singleClauseExpression rejection, singleClauseExpression success with
        | Some failure, Some value when isFailureCall failure ->
            validationSuccess value |> Option.map (fun result -> condition, result)
        | _ -> None
    | _ -> None

/// Read a rejecting if guard followed by an unconditional success clause.
let private ifValidation decision =
    let clauses =
        nodeNamedChildren decision
        |> List.filter (fun child -> nodeType child = NodeType "if_clause")

    match clauses with
    | [ rejection; success ] ->
        match
            nodeField "guard" rejection,
            nodeField "guard" success,
            singleClauseExpression rejection,
            singleClauseExpression success
        with
        | Some condition, Some fallback, Some failure, Some value when
            nodeText condition <> trueAtom
            && nodeText fallback = trueAtom
            && isFailureCall failure
            ->
            validationSuccess value |> Option.map (fun result -> condition, result)
        | _ -> None
    | _ -> None

/// Extract a local Erlang validation whose rejecting and success paths are both explicit.
///
/// decision: require one decision expression and a terminating BIF rejection so ordinary boolean
/// queries and clauses with intervening work do not masquerade as validators.
let extract (head: FunctionHead) : GuardedValidation option =
    match nodeField "body" head.Body with
    | Some body when nodeField "guard" head.ParametersRoot |> Option.isNone ->
        let result =
            match nodeNamedChildren body with
            | [ decision ] ->
                match nodeType decision with
                | NodeType "case_expr" -> caseValidation decision |> Option.map (fun value -> decision, value)
                | NodeType "if_expr" -> ifValidation decision |> Option.map (fun value -> decision, value)
                | _ -> None
            | _ -> None

        result
        |> Option.map (fun (decision, (condition, success)) ->
            {
                Anchor = decision
                Condition = condition
                Success = success
                IdentityReturnPolicy = AcceptUntypedParameter
            })
    | _ -> None
