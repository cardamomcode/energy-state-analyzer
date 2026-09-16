module Energy.Languages.CSharp

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

let private blockNodeType = NodeType "block"
let private tryStatementNodeType = NodeType "try_statement"
let private catchClauseNodeType = NodeType "catch_clause"
let private finallyClauseNodeType = NodeType "finally_clause"

/// Name C#'s lambda-expression node type.
let private lambdaExpressionNodeType = NodeType "lambda_expression"

/// Name C#'s anonymous-method node type.
let private anonymousMethodNodeType = NodeType "anonymous_method_expression"

/// Name C#'s parenthesized parameter container.
let private parameterListNodeType = NodeType "parameter_list"

/// Keep the existing set of C# named-function forms explicit.
let private namedFunctionNodeTypes =
    Set.ofList
        [ NodeType "method_declaration"
          NodeType "constructor_declaration"
          NodeType "local_function_statement" ]

/// Extract parenthesized or single implicit C# parameters.
let private parametersOf (node: Node) =
    match nodeField "parameters" node with
    | Some parameters when nodeType parameters = NodeType "implicit_parameter" -> [ parameters ]
    | Some parameters ->
        nodeChildren parameters
        |> List.filter (fun child -> nodeType child = NodeType "parameter")
    | None -> []

/// Classify a C# anonymous callable directly assigned to a class field or local.
let private anonymousBinding (node: Node) : CallableRole * string option =
    match nodeParent node with
    | Some declarator when nodeType declarator = NodeType "variable_declarator" ->
        let bindingName = nodeField "name" declarator |> Option.map nodeText

        let isClassMember =
            declarator
            |> nodeParent
            |> Option.bind nodeParent
            |> Option.filter (fun parent -> nodeType parent = NodeType "field_declaration")
            |> Option.bind nodeParent
            |> Option.exists (fun parent -> nodeType parent = NodeType "declaration_list")

        if isClassMember then
            BoundAnonymous ClassMemberBinding, bindingName
        else
            InlineAnonymous, bindingName
    | _ -> InlineAnonymous, None

/// Select the explicit C# body field or final block when the grammar omits that field.
let private callableBody (node: Node) =
    nodeField "body" node
    |> Option.orElseWith (fun () -> nodeNamedChildren node |> List.tryLast)
    |> Option.defaultValue node

/// Normalize C# named functions, lambdas, and anonymous methods into callable views.
let private callableViews (node: Node) : CallableView list =
    if Set.contains (nodeType node) namedFunctionNodeTypes then
        [ { Anchor = node
            Body = callableBody node
            Role = NamedDefinition
            BindingName = nodeField "name" node |> Option.map nodeText
            Parameters = parametersOf node } ]
    elif
        nodeType node = lambdaExpressionNodeType
        || nodeType node = anonymousMethodNodeType
    then
        let role, bindingName = anonymousBinding node

        [ { Anchor = node
            Body = callableBody node
            Role = role
            BindingName = bindingName
            Parameters = parametersOf node } ]
    else
        []

let private bodyItems (node: Node) : Node list =
    let children = nodeNamedChildren node

    match children |> List.tryFind (fun child -> nodeType child = blockNodeType) with
    | Some block -> nodeNamedChildren block
    | None -> children

/// Preserve protected work, combined recovery work, and individual handler or cleanup bodies.
let private errorHandlingRegion (node: Node) : ErrorHandlingRegion option =
    if nodeType node <> tryStatementNodeType then
        None
    else
        let children = nodeNamedChildren node

        children
        |> List.tryFind (fun child -> nodeType child = blockNodeType)
        |> Option.map (fun protectedBody ->
            { Anchor = node
              ProtectedBody = nodeNamedChildren protectedBody
              ProtectedItems = nodeNamedChildren protectedBody
              RecoveryItems =
                children
                |> List.filter (fun child ->
                    nodeType child = catchClauseNodeType || nodeType child = finallyClauseNodeType)
                |> List.collect bodyItems
              RecoveryBodies =
                children
                |> List.filter (fun child ->
                    nodeType child = catchClauseNodeType || nodeType child = finallyClauseNodeType)
                |> List.map (fun clause ->
                    { Anchor = clause
                      Items = bodyItems clause }) })

let private switchBranchCount (node: Node) : int option =
    if nodeType node <> NodeType "switch_statement" then
        None
    else
        let sections =
            nodeNamedChildren node
            |> List.collect nodeNamedChildren
            |> List.filter (fun child -> nodeType child = NodeType "switch_section")

        let hasFallback =
            sections
            |> List.exists (fun section -> (nodeText section).StartsWith("default:", System.StringComparison.Ordinal))

        Some(sections.Length + if hasFallback then 0 else 1)

let private equalityComparisons (node: Node) : EqualityComparison list =
    if nodeType node <> NodeType "binary_expression" then
        []
    else
        match nodeChildren node |> List.tryFind (fun child -> nodeType child = NodeType "==") with
        | Some operatorNode ->
            match
                nodeChildren node
                |> List.filter (fun child -> nodeId child <> nodeId operatorNode)
            with
            | [ left; right ] -> [ { Left = left; Right = right } ]
            | _ -> []
        | None -> []

let private extractTypedParameter (node: Node) : TypedParameter option =
    if nodeType node <> NodeType "parameter" then
        None
    else
        match nodeNamedChildren node with
        | declaredType :: name :: _ ->
            Some
                { Name = nodeText name
                  Type = nodeText declaredType }
        | _ -> None

let private extractReturnType (node: Node) : string option =
    if nodeType node <> NodeType "method_declaration" then
        None
    else
        nodeNamedChildren node
        |> List.tryFind (fun child ->
            nodeType child <> NodeType "modifier"
            && nodeType child <> NodeType "attribute_list")
        |> Option.map nodeText

let private baseClassNames (node: Node) : string list =
    nodeChildren node
    |> List.tryFind (fun child -> nodeType child = NodeType "base_list")
    |> Option.map nodeNamedChildren
    |> Option.defaultValue []
    |> List.map nodeText

/// The C# LanguageAdapter for the official tree-sitter-c-sharp v0.23.5 grammar.
///
/// decision: treats methods, constructors, and local functions as function definitions because all
/// three expose a parameter list and body whose complexity affects the surrounding C# program.
/// invariant: interpolated strings stay outside StringLiteral because the grammar represents them as
/// interpolated_string_expression rather than ordinary string_literal nodes.
let cSharpLanguageAdapter: LanguageAdapter =
    { Id = "csharp"
      GrammarPath = "grammars/tree-sitter-c-sharp.wasm"
      NodeTypes =
        { Block = Some blockNodeType
          Parameters = parameterListNodeType
          IfStatement = Some(NodeType "if_statement")
          ElseClause = None
          ForStatement = Some(NodeType "for_statement")
          WhileStatement = Some(NodeType "while_statement")
          ConditionalExpression = Some(NodeType "conditional_expression")
          Lambda = Some lambdaExpressionNodeType
          ImportStatement = Some(NodeType "using_directive")
          ImportFromStatement = None
          ExpressionStatement = Some(NodeType "expression_statement")
          Assignment = Some(NodeType "variable_declaration")
          Module = Some(NodeType "compilation_unit")
          ExportStatement = None
          Comment = Some(NodeType "comment")
          IntegerLiteral = Some(NodeType "integer_literal")
          FloatLiteral = Some(NodeType "real_literal")
          StringLiteral = Some(NodeType "string_literal") }
      IsFunctionDefinition = fun node -> Set.contains (nodeType node) namedFunctionNodeTypes
      GetFunctionHeads = fun node -> [ { ParametersRoot = node; Body = node } ]
      GetCallableViews = callableViews
      IsStaticMethod = fun node -> nodeChildren node |> List.exists (fun child -> nodeText child = "static")
      ParameterChildTypes = [ NodeType "parameter" ]
      DecisionNodeTypes =
        [ NodeType "if_statement"
          NodeType "for_statement"
          NodeType "foreach_statement"
          NodeType "while_statement"
          NodeType "do_statement"
          NodeType "catch_clause"
          NodeType "conditional_expression"
          NodeType "switch_statement" ]
      CyclomaticBranchCount = switchBranchCount
      GetCognitiveStructure =
        CognitiveSyntax.classify
            [ NodeType "if_statement"
              NodeType "for_statement"
              NodeType "foreach_statement"
              NodeType "while_statement"
              NodeType "do_statement"
              NodeType "catch_clause"
              NodeType "switch_statement" ]
      NestingControlTypes =
        [ NodeType "if_statement"
          NodeType "for_statement"
          NodeType "foreach_statement"
          NodeType "while_statement"
          NodeType "do_statement"
          NodeType "try_statement"
          NodeType "switch_statement" ]
      GetBooleanOperator =
        fun node ->
            if nodeType node <> NodeType "binary_expression" then
                None
            else
                nodeChildren node
                |> List.tryPick (fun child ->
                    match nodeType child with
                    | NodeType "&&" -> Some And
                    | NodeType "||" -> Some Or
                    | _ -> None)
      IsTryElseClause = fun _ -> false
      VariableReferenceNodeTypes = [ NodeType "identifier"; NodeType "member_access_expression" ]
      ExtractTypedParameter = extractTypedParameter
      ExtractReturnType = extractReturnType
      GenericBrackets = { Open = "<"; Close = ">" }
      PrimitiveTypeNames =
        Set.ofList
            [ "bool"
              "byte"
              "char"
              "decimal"
              "double"
              "float"
              "int"
              "long"
              "sbyte"
              "short"
              "string"
              "uint"
              "ulong"
              "ushort" ]
      KeywordOnlyBoundaryTypes = []
      DistinctTypeAdvice = "a readonly record struct or enum"
      GetEqualityComparisons = equalityComparisons
      GetMatchStringCases = fun _ -> None
      GetMembershipComparisons = fun _ -> []
      IsMatchCaseLiteral =
        fun node ->
            nodeType node = NodeType "integer_literal"
            || nodeType node = NodeType "character_literal"
      GetElseIfBranches = fun _ -> []
      SubscriptNodeTypes = [ NodeType "element_access_expression" ]
      IsFormattedOrInterpolatedString = fun _ -> false
      IsDefaultParameterValue =
        fun node ->
            match nodeParent node with
            | Some parent when nodeType parent = NodeType "parameter" ->
                let children = nodeChildren parent

                List.exists (fun child -> nodeType child = NodeType "=") children
                && (children
                    |> List.tryLast
                    |> Option.exists (fun last -> nodeId last = nodeId node))
            | _ -> false
      IsBooleanLiteral = fun node -> nodeType node = NodeType "boolean_literal"
      IsPositionalCallArgument =
        fun node ->
            match nodeParent node with
            | Some argument when nodeType argument = NodeType "argument" ->
                match nodeParent argument with
                | Some arguments when nodeType arguments = NodeType "argument_list" ->
                    match nodeParent arguments with
                    | Some invocation -> nodeType invocation = NodeType "invocation_expression"
                    | None -> false
                | None -> false
                | _ -> false
            | _ -> false
      IsExplicitConstant =
        fun node ->
            nodeType node = NodeType "field_declaration"
            && (nodeChildren node |> List.exists (fun child -> nodeText child = "const"))
      ImportInfo =
        fun node ->
            let source =
                nodeNamedChildren node
                |> List.tryLast
                |> Option.map nodeText
                |> Option.defaultValue (nodeText node)

            [ { Kind = Module
                Source = source
                Bindings = [] } ]
      IsClassDefinition =
        fun node ->
            nodeType node = NodeType "class_declaration"
            || nodeType node = NodeType "struct_declaration"
            || nodeType node = NodeType "record_declaration"
            || nodeType node = NodeType "interface_declaration"
      GetClassName =
        fun node ->
            nodeChildren node
            |> List.tryFind (fun child -> nodeType child = NodeType "identifier")
            |> Option.map nodeText
      GetBaseClassNames = baseClassNames
      GetErrorHandlingRegion = errorHandlingRegion
      GetFunctionLogicalItems = bodyItems
      GetGuardedValidation =
        ValidationSyntax.extract
            { Containers = [ NodeType "block" ]
              Conditional = NodeType "if_statement"
              Rejections = [ NodeType "throw_statement" ]
              Return = Some(NodeType "return_statement")
              FailureCalls = []
              EmptyValues = []
              IsNonExecutable = fun _ -> false
              PreservesCheckedInformation = fun node -> nodeType node = NodeType "constructor_declaration" } }
