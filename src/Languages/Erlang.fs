module Energy.Languages.Erlang

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Name the grammar node for a named function clause.
let private functionClause = NodeType "function_clause"

/// Name the grammar node that wraps one named function clause.
let private funDeclaration = NodeType "fun_decl"

/// Name the grammar node that wraps anonymous fun clauses.
let private anonymousFun = NodeType "anonymous_fun"

/// Name the grammar node for a clause's expression sequence.
let private clauseBody = NodeType "clause_body"

/// Name the grammar node for a callable's argument list.
let private expressionArguments = NodeType "expr_args"

/// Find the clause carrying an Erlang function declaration.
let private declarationClause node =
    nodeField "clause" node
    |> Option.filter (fun clause -> nodeType clause = functionClause)

/// Extract the arguments of a named or anonymous Erlang function clause.
let private parametersOf clause =
    nodeField "args" clause
    |> Option.map nodeNamedChildren
    |> Option.defaultValue []

/// Return the expression sequence owned by a clause body.
let private bodyItems node =
    if nodeType node = clauseBody then
        nodeNamedChildren node
    else
        let body =
            if nodeType node = funDeclaration then
                declarationClause node |> Option.bind (nodeField "body")
            else
                nodeField "body" node

        body
        |> Option.map nodeNamedChildren
        |> Option.defaultValue (nodeNamedChildren node)

/// Give each Erlang clause an independently analyzable callable view.
///
/// decision: a `fun_decl` owns one `function_clause`; repeated clauses are separate grammar forms,
/// so each gets a view anchored on its own source range. This keeps parameters, guards, and bodies
/// attached to the right clause without crossing into the next declaration.
let private namedCallable node clause =
    {
        Anchor = node
        Body = nodeField "body" clause |> Option.defaultValue clause
        ReturnTypeRoot = node
        Role = NamedDefinition
        BindingName = nodeField "name" clause |> Option.map nodeText
        Parameters = parametersOf clause
    }

/// Give each anonymous fun clause an independently analyzable callable view.
let private anonymousCallable clause =
    {
        Anchor = clause
        Body = nodeField "body" clause |> Option.defaultValue clause
        ReturnTypeRoot = clause
        Role = InlineAnonymous
        BindingName = nodeField "name" clause |> Option.map nodeText
        Parameters = parametersOf clause
    }

/// Normalize named and anonymous Erlang function clauses for shared detectors.
let private callableViews node : CallableView list =
    match nodeType node with
    | kind when kind = funDeclaration -> declarationClause node |> Option.map (namedCallable node) |> Option.toList
    | kind when kind = anonymousFun ->
        nodeNamedChildren node
        |> List.filter (fun child -> nodeType child = NodeType "fun_clause")
        |> List.map anonymousCallable
    | _ -> []

/// Count the outcomes of Erlang's clause-based dispatch forms.
let private branchCount node =
    let clauses =
        nodeNamedChildren node
        |> List.filter (fun child -> List.contains (nodeType child) [ NodeType "cr_clause"; NodeType "if_clause" ])

    match nodeType node with
    | NodeType "case_expr" ->
        let exhaustive =
            clauses
            |> List.exists (fun clause ->
                nodeField "guard" clause |> Option.isNone
                && (nodeField "pat" clause
                    |> Option.exists (fun pattern -> nodeType pattern = NodeType "var")))

        Some(clauses.Length + if exhaustive then 0 else 1)
    | NodeType "if_expr" ->
        let exhaustive =
            clauses
            |> List.exists (fun clause ->
                nodeField "guard" clause
                |> Option.exists (fun guard ->
                    nodeNamedChildren guard |> List.exists (fun item -> nodeText item = "true")))

        Some(clauses.Length + if exhaustive then 0 else 1)
    | NodeType "receive_expr" ->
        let after = nodeField "after" node |> Option.isSome
        Some(clauses.Length + if after then 1 else 0)
    | _ -> None

/// Model Erlang clauses as one branching construct with nested arm bodies.
let private cognitiveStructure node =
    match nodeType node with
    | NodeType "case_expr"
    | NodeType "if_expr"
    | NodeType "receive_expr" ->
        let bodies =
            nodeNamedChildren node
            |> List.filter (fun child ->
                List.contains (nodeType child) [ NodeType "cr_clause"; NodeType "if_clause"; NodeType "receive_after" ])

        Some(Flow(Structural, bodies))
    | NodeType "catch_clause" -> nodeField "body" node |> Option.map (fun body -> Flow(Structural, [ body ]))
    | kind when kind = anonymousFun -> Some Closure
    | NodeType "paren_expr" -> nodeField "expr" node |> Option.map BooleanGroup
    | _ -> None

/// Extract Erlang short-circuit boolean operators from binary expressions.
let private booleanOperator node =
    if nodeType node <> NodeType "binary_op_expr" then
        None
    else
        nodeChildren node
        |> List.tryPick (fun child ->
            match nodeText child with
            | "andalso" -> Some And
            | "orelse" -> Some Or
            | _ -> None)

/// Extract equality comparisons without treating pattern matching as equality.
let private equalityComparisons node =
    if nodeType node <> NodeType "binary_op_expr" then
        []
    else
        let operator =
            nodeChildren node
            |> List.exists (fun child -> List.contains (nodeText child) [ "=="; "=:=" ])

        match nodeField "lhs" node, nodeField "rhs" node with
        | Some left, Some right when operator -> [ { Left = left; Right = right } ]
        | _ -> []

/// Extract imports declared by Erlang's explicit `-import` attribute.
let private importInfo node =
    match nodeType node with
    | NodeType "import_attribute" ->
        nodeField "module" node
        |> Option.map (fun source ->
            {
                Kind = Module
                Source = nodeText source
                Bindings =
                    nodeNamedChildren node
                    |> List.filter (fun child -> nodeType child = NodeType "fa")
                    |> List.choose (fun item ->
                        nodeField "fun" item
                        |> Option.map (fun name ->
                            {
                                ImportedName = nodeText name
                                LocalName = nodeText name
                            }))
            })
        |> Option.toList
    | NodeType "pp_include"
    | NodeType "pp_include_lib" ->
        nodeNamedChildren node
        |> List.filter (fun child -> nodeType child = NodeType "string")
        |> List.map (fun path ->
            {
                Kind = Header
                Source = (nodeText path).Trim('"')
                Bindings = []
            })
    | _ -> []

/// Preserve the protected and recovery expressions of an Erlang try.
let private errorHandlingRegion node : ErrorHandlingRegion option =
    if
        nodeType node <> NodeType "try_expr"
        || (nodeNamedChildren node
            |> List.exists (fun child -> nodeType child = NodeType "cr_clause"))
    then
        // decision: `try ... of` has a separate success continuation not represented by the
        // shared error-region denominator. Skip it until that continuation can be counted.
        None
    else
        let children = nodeNamedChildren node

        let catchClauses =
            children |> List.filter (fun child -> nodeType child = NodeType "catch_clause")

        let afterClauses =
            children |> List.filter (fun child -> nodeType child = NodeType "try_after")

        let recovery = catchClauses @ afterClauses

        if recovery.IsEmpty then
            None
        else
            let protectedItems =
                children
                |> List.takeWhile (fun child ->
                    not (
                        List.contains
                            (nodeType child)
                            [ NodeType "cr_clause"; NodeType "catch_clause"; NodeType "try_after" ]
                    ))

            Some
                {
                    Anchor = node
                    ProtectedBody = protectedItems
                    ProtectedItems = protectedItems
                    RecoveryItems = recovery |> List.collect bodyItems
                    RecoveryBodies =
                        recovery
                        |> List.map (fun clause ->
                            {
                                Anchor = clause
                                Items = bodyItems clause
                            })
                }

/// Adapt the WhatsApp tree-sitter-erlang 0.21.0 grammar to the shared detector contract.
///
/// decision: Erlang atoms `true` and `false` are boolean literals only by their exact text; other
/// atoms remain symbolic values and cannot be treated as strings or booleans by the detectors.
let erlangLanguageAdapter: LanguageAdapter =
    {
        Id = "erlang"
        GrammarPath = "grammars/tree-sitter-erlang.wasm"
        NodeTypes =
            {
                Block = Some clauseBody
                Parameters = expressionArguments
                // Erlang `if` is a clause dispatch; detectors expecting an if/else tree do not
                // apply. Complexity and nesting still see it through the control-flow hooks.
                IfStatement = None
                ElseClause = None
                ForStatement = None
                WhileStatement = None
                ConditionalExpression = None
                Lambda = Some anonymousFun
                ImportStatement = Some(NodeType "import_attribute")
                ImportFromStatement = Some(NodeType "pp_include")
                AdditionalImportTypes = [ NodeType "pp_include_lib" ]
                ExpressionStatement = None
                Assignment = Some(NodeType "match_expr")
                Module = Some(NodeType "source_file")
                ExportStatement = None
                Comment = Some(NodeType "comment")
                IntegerLiteral = Some(NodeType "integer")
                FloatLiteral = Some(NodeType "float")
                StringLiteral = Some(NodeType "string")
            }
        IsFunctionDefinition = fun node -> nodeType node = funDeclaration
        GetFunctionHeads =
            fun node ->
                declarationClause node
                |> Option.map (fun clause ->
                    {
                        ParametersRoot = clause
                        Body = clause
                    })
                |> Option.toList
        GetCallableViews = callableViews
        ParameterChildTypes =
            [
                NodeType "var"
                NodeType "atom"
                NodeType "tuple"
                NodeType "list"
                NodeType "match_expr"
            ]
        DecisionNodeTypes =
            [
                NodeType "case_expr"
                NodeType "if_expr"
                NodeType "receive_expr"
                NodeType "catch_clause"
            ]
        CyclomaticBranchCount = branchCount
        GetCognitiveStructure = cognitiveStructure
        NestingControlTypes =
            [
                NodeType "case_expr"
                NodeType "if_expr"
                NodeType "receive_expr"
                NodeType "try_expr"
            ]
        GetBooleanOperator = booleanOperator
        IsTryElseClause = fun _ -> false
        VariableReferenceNodeTypes = [ NodeType "var" ]
        ExtractTypedParameter = fun _ -> None
        ExtractReturnType = fun _ -> None
        GenericBrackets = { Open = "<"; Close = ">" }
        PrimitiveTypeNames = Set.empty
        KeywordOnlyBoundaryTypes = []
        DistinctTypeAdvice = "a tagged tuple or record"
        GetEqualityComparisons = equalityComparisons
        GetMatchStringCases = fun _ -> None
        GetMembershipComparisons = fun _ -> []
        IsMatchCaseLiteral = fun node -> nodeType node = NodeType "atom" || nodeType node = NodeType "integer"
        GetElseIfBranches = fun _ -> []
        SubscriptNodeTypes = []
        IsFormattedOrInterpolatedString = fun _ -> false
        IsDefaultParameterValue = fun _ -> false
        IsStructuralNumber =
            fun node ->
                nodeParent node
                |> Option.exists (fun parent -> nodeType parent = NodeType "arity")
        IsBooleanLiteral =
            fun node ->
                nodeType node = NodeType "atom"
                && List.contains (nodeText node) [ "true"; "false" ]
        IsPositionalCallArgument =
            fun node ->
                nodeParent node
                |> Option.exists (fun parent ->
                    nodeType parent = expressionArguments
                    && (nodeParent parent |> Option.exists (fun call -> nodeType call = NodeType "call")))
        IsExplicitConstant = fun node -> nodeType node = NodeType "pp_define"
        ImportInfo = importInfo
        IsClassDefinition = fun _ -> false
        IsStaticMethod = fun _ -> false
        GetClassName = fun _ -> None
        GetBaseClassNames = fun _ -> []
        GetErrorHandlingRegion = errorHandlingRegion
        GetFunctionLogicalItems = bodyItems
        GetGuardedValidation = ErlangValidation.extract
    }
