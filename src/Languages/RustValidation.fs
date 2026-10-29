module Energy.Languages.RustValidation

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Name the payload-free Option rejection variant used in signature and branch matching.
let private noneVariant = "None"

/// Omit comments while counting executable Rust expressions.
let private expressions node =
    nodeNamedChildren node
    |> List.filter (fun child ->
        not (List.contains (nodeType child) [ NodeType "line_comment"; NodeType "block_comment" ]))

/// Unwrap an expression statement or return without descending through other control flow.
let rec private unwrap node =
    if List.contains (nodeType node) [ NodeType "expression_statement"; NodeType "return_expression" ] then
        match expressions node with
        | [ value ] -> unwrap value
        | _ -> node
    else
        node

/// Recognize an unqualified error carrier in a function signature.
let private carrier head =
    nodeField "return_type" head.ParametersRoot
    |> Option.bind (fun returnType ->
        if nodeType returnType = NodeType "generic_type" then
            match expressions returnType with
            | name :: _ when nodeType name = NodeType "type_identifier" ->
                match nodeText name with
                | "Result" -> Some("Err", "Ok")
                | "Option" -> Some(noneVariant, "Some")
                | _ -> None
            | _ -> None
        else
            None)

/// Read only a direct variant constructor with one payload.
let private variantPayload variant node =
    let value = unwrap node

    if nodeType value = NodeType "call_expression" then
        match nodeField "function" value, nodeField "arguments" value with
        | Some name, Some arguments when nodeType name = NodeType "identifier" && nodeText name = variant ->
            match expressions arguments with
            | [ payload ] -> Some payload
            | _ -> None
        | _ -> None
    else
        None

/// Require a complete, direct rejection rather than a fallible call or nested return.
let private rejects variant node =
    let value = unwrap node

    if variant = noneVariant then
        nodeType value = NodeType "identifier" && nodeText value = noneVariant
    else
        variantPayload variant value |> Option.isSome

/// Read an unchanged checked value from the success variant.
let private success variant node =
    variantPayload variant node
    |> Option.filter (fun value -> nodeType value = NodeType "identifier")
    |> Option.map UnchangedInput

/// Require the guard body to contain one direct early return.
let private earlyRejects variant body =
    match expressions body with
    | [ statement ] when nodeType statement = NodeType "expression_statement" ->
        match expressions statement with
        | [ returned ] when nodeType returned = NodeType "return_expression" -> rejects variant returned
        | _ -> false
    | _ -> false

/// Extract one early-return guard followed immediately by its successful result.
let private guardedIf rejection successVariant items =
    match items with
    | [ guard; returned ] when nodeType (unwrap guard) = NodeType "if_expression" ->
        let decision = unwrap guard

        match nodeField "condition" decision, nodeField "consequence" decision, nodeField "alternative" decision with
        | Some condition, Some body, None when earlyRejects rejection body ->
            success successVariant returned
            |> Option.map (fun result -> decision, condition, result)
        | _ -> None
    | _ -> None

/// Extract a selective rejection and unguarded fallback from one two-arm match.
let private matched rejection successVariant items =
    match items with
    | [ statement ] when nodeType (unwrap statement) = NodeType "match_expression" ->
        let decision = unwrap statement

        let arms =
            nodeField "body" decision |> Option.map expressions |> Option.defaultValue []

        match nodeField "value" decision, arms with
        | Some condition, [ invalid; valid ] ->
            let rejectPattern = nodeField "pattern" invalid
            let fallbackPattern = nodeField "pattern" valid

            let selective pattern =
                // decision: tree-sitter puts an arm guard inside match_pattern; a second named
                // child makes the rejection conditional and cannot establish the checked fact.
                match expressions pattern with
                | [ value ] ->
                    List.contains
                        (nodeType value)
                        [
                            NodeType "integer_literal"
                            NodeType "char_literal"
                            NodeType "string_literal"
                            NodeType "boolean_literal"
                            NodeType "slice_pattern"
                            NodeType "tuple_pattern"
                        ]
                | _ -> false

            match rejectPattern, fallbackPattern, nodeField "value" invalid, nodeField "value" valid with
            | Some pattern, Some fallback, Some failure, Some returned when
                selective pattern && nodeText fallback = "_" && rejects rejection failure
                ->
                success successVariant returned
                |> Option.map (fun result -> decision, condition, result)
            | _ -> None
        | _ -> None
    | _ -> None

/// Recognize Rust validation only when a standard error variant and an unchanged success payload agree.
///
/// decision: a Result or Option return alone says nothing about the checked property's type;
/// require a local rejecting branch and the original input inside Ok or Some.
let extract (head: FunctionHead) : GuardedValidation option =
    match carrier head with
    | Some(rejection, successVariant) ->
        let items = expressions head.Body

        let candidate =
            match guardedIf rejection successVariant items with
            | Some result -> Some result
            | None -> matched rejection successVariant items

        candidate
        |> Option.map (fun (anchor, condition, result) ->
            {
                Anchor = anchor
                Condition = condition
                Success = result
                IdentityReturnPolicy = AcceptUntypedParameter
            })
    | None -> None
