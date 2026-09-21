module Energy.Languages.ValidationSyntax

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Grammar shapes needed to recognize a straight-line guard with an unchanged, absent, or
/// bare-boolean success value.
type GuardSyntax =
    {
        Containers: NodeType list
        Conditional: NodeType
        Rejections: NodeType list
        Return: NodeType option
        FailureCalls: string list
        EmptyValues: string list
        IsNonExecutable: Node -> bool
        // The polarity of a boolean literal, when this node is one (Python/TS/C++ `true`/`false`
        // nodes, C# `boolean_literal`, F# `const` wrapping a `bool` child, Kotlin `true`/`false`
        // identifier tokens). Read as `Some LiteralFalse` in `rejects` (a falsy return is a
        // rejection) and `Some _` in the success classification (any literal boolean success is a
        // bare verdict). Mirrors each adapter's IsBooleanLiteral hook while returning the polarity
        // the guard logic needs.
        BooleanLiteralValue: Node -> BooleanLiteralPolarity option
        // The optional condition is absent during the signature/body-level precheck and present once
        // a guarded validation has been extracted. Language contracts that apply to a particular
        // parameter use the latter to avoid suppressing checks of unrelated parameters.
        PreservesCheckedInformation: Node -> Node option -> bool
    }

/// Discard comments and non-executable statements while retaining every executable statement.
let private children syntax node =
    nodeNamedChildren node
    |> List.filter (fun child ->
        not (List.contains (nodeType child) [ NodeType "comment"; NodeType "line_comment"; NodeType "block_comment" ])
        && not (syntax.IsNonExecutable child))

/// Unwrap only grammar containers, never loops, handlers, or nested declarations.
let rec private statements syntax node =
    if List.contains (nodeType node) syntax.Containers then
        children syntax node |> List.collect (statements syntax)
    else
        [ node ]

/// Recognize F# failure applications by their leftmost callee, not text inside arguments.
let rec private callee syntax node =
    if nodeType node = NodeType "application_expression" then
        children syntax node |> List.tryHead |> Option.bind (callee syntax)
    else
        Some(nodeText node)

/// Establish that the entire branch rejects; a nested conditional throw is insufficient.
///
/// decision: a single falsy-literal return is a rejection alongside throw/raise/fail branches —
/// the bool-rejecting guard — while a truthy return stays a non-rejection, so flipped branches
/// (`if ok: return True`) never extract.
let private rejects syntax node =
    let falsyReturn statement =
        match syntax.Return with
        | Some kind when nodeType statement = kind ->
            match children syntax statement with
            | [ value ] -> syntax.BooleanLiteralValue value = Some LiteralFalse
            | _ -> false
        | _ -> false

    match statements syntax node with
    | [ statement ] ->
        List.contains (nodeType statement) syntax.Rejections
        || (nodeType statement = NodeType "application_expression"
            && (callee syntax statement
                |> Option.exists (fun name -> List.contains name syntax.FailureCalls)))
        || falsyReturn statement
    | _ -> false

/// Classify a decoded success value: a boolean literal is a bare verdict, everything else is
/// the unchanged input.
let private classify syntax value =
    match syntax.BooleanLiteralValue value with
    | Some _ -> BareBoolean value
    | None -> UnchangedInput value

/// Decode explicit returns separately from implicit fallthrough.
let private success syntax returned =
    let value =
        match syntax.Return with
        | Some kind when nodeType returned = kind ->
            match children syntax returned with
            | [] -> Some NoValue
            | [ value ] ->
                if List.contains (nodeText value) syntax.EmptyValues then
                    Some NoValue
                else
                    Some(classify syntax value)
            | _ -> None
        | None ->
            if List.contains (nodeText returned) syntax.EmptyValues then
                Some NoValue
            else
                Some(classify syntax returned)
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
        children syntax head.Body
        |> List.filter (fun node ->
            List.contains (nodeType node) syntax.Containers
            || nodeType node = syntax.Conditional)
        |> List.collect (statements syntax)

/// Extract a rejecting guard with an identity return, no success value, or a bare-boolean
/// success value.
///
/// decision: accepts only a guard and optional return so mutation, recovery, and intervening work cannot masquerade as a check-only validator.
let extract syntax (head: FunctionHead) : GuardedValidation option =
    let candidate =
        match
            (if syntax.PreservesCheckedInformation head.ParametersRoot None then
                 []
             else
                 bodyItems syntax head)
        with
        | [ guard ] -> Some(guard, NoValue)
        | [ guard; returned ] -> success syntax returned |> Option.map (fun result -> guard, result)
        | _ -> None

    let extracted =
        candidate
        |> Option.bind (fun (guard, result) ->
            if nodeType guard <> syntax.Conditional then
                None
            else
                match children syntax guard with
                | [ condition; rejection ] when rejects syntax rejection ->
                    Some
                        {
                            Anchor = guard
                            Condition = condition
                            Success = result
                        }
                // F#/Kotlin single-expression `if condition then false else <success>`: the then
                // branch must be a bare falsy literal. Statement grammars are safe — their if
                // children are blocks or else-clauses, never bare literals, so only expression
                // grammars' bare-branch shape can match here. The else branch is itself the success
                // value (a bare expression, not a return statement), so it is classified directly.
                | [ condition; thenBranch; elseBranch ] when syntax.BooleanLiteralValue thenBranch = Some LiteralFalse ->
                    Some
                        {
                            Anchor = guard
                            Condition = condition
                            Success = classify syntax elseBranch
                        }
                | _ -> None)

    extracted
    |> Option.filter (fun candidate ->
        not (syntax.PreservesCheckedInformation head.ParametersRoot (Some candidate.Condition)))
