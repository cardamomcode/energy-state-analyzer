module Energy.Languages.CPlusPlus

open System
open System.Text.RegularExpressions

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

let private tryStatementNodeType = NodeType "try_statement"
let private compoundStatementNodeType = NodeType "compound_statement"
let private catchClauseNodeType = NodeType "catch_clause"

/// Name C++'s named-function definition node type.
let private functionDefinitionNodeType = NodeType "function_definition"

/// Name C++'s lambda-expression node type.
let private lambdaExpressionNodeType = NodeType "lambda_expression"

/// Name the C++ parameter-list node nested below recursive declarators.
let private parameterListNodeType = NodeType "parameter_list"

/// Keep the existing C++ parameter-declaration contract in one reusable list.
let private parameterChildTypes =
    [ NodeType "parameter_declaration"
      NodeType "optional_parameter_declaration"
      NodeType "variadic_parameter_declaration" ]

let private bodyItems (node: Node) : Node list =
    let children = nodeNamedChildren node

    match
        children
        |> List.tryFind (fun child -> nodeType child = compoundStatementNodeType)
    with
    | Some body -> nodeNamedChildren body
    | None -> children

/// Preserve protected work, combined recovery work, and individual handler or cleanup bodies.
let private errorHandlingRegion (node: Node) : ErrorHandlingRegion option =
    if nodeType node <> tryStatementNodeType then
        None
    else
        let children = nodeNamedChildren node

        children
        |> List.tryFind (fun child -> nodeType child = compoundStatementNodeType)
        |> Option.map (fun protectedBody ->
            { Anchor = node
              ProtectedBody = nodeNamedChildren protectedBody
              ProtectedItems = nodeNamedChildren protectedBody
              RecoveryItems =
                children
                |> List.filter (fun child -> nodeType child = catchClauseNodeType)
                |> List.collect bodyItems
              RecoveryBodies =
                children
                |> List.filter (fun child -> nodeType child = catchClauseNodeType)
                |> List.map (fun clause ->
                    { Anchor = clause
                      Items = bodyItems clause }) })

/// The C++ LanguageAdapter. Grammar node names and shapes below target the official
/// tree-sitter-cpp v0.23.4 WASM bundled in grammars/; its checksum and license are recorded beside
/// the artifact. C++ declarators are recursive, so parameter extraction deliberately separates the
/// direct type specifier from the declarator shape instead of assuming a flat `type name` pair.

let private typeNodeTypes =
    Set.ofList
        [ NodeType "primitive_type"
          NodeType "sized_type_specifier"
          NodeType "type_identifier"
          NodeType "qualified_identifier"
          NodeType "template_type"
          NodeType "placeholder_type_specifier"
          NodeType "decltype"
          NodeType "dependent_type" ]

let private declaratorNodeTypes =
    Set.ofList
        [ NodeType "identifier"
          NodeType "field_identifier"
          NodeType "pointer_declarator"
          NodeType "reference_declarator"
          NodeType "array_declarator"
          NodeType "function_declarator"
          NodeType "parenthesized_declarator" ]

let rec private tryFindDescendant (predicate: Node -> bool) (node: Node) : Node option =
    if predicate node then
        Some node
    else
        nodeChildren node |> List.tryPick (tryFindDescendant predicate)

let private isTypeNode (node: Node) =
    Set.contains (nodeType node) typeNodeTypes

let private isDeclaratorNode (node: Node) =
    Set.contains (nodeType node) declaratorNodeTypes

/// Extract explicit C++ parameters while excluding lambda captures.
let private parametersOf (node: Node) =
    tryFindDescendant (fun child -> nodeType child = parameterListNodeType) node
    |> Option.map (fun parameters ->
        nodeChildren parameters
        |> List.filter (fun child -> parameterChildTypes |> List.contains (nodeType child)))
    |> Option.defaultValue []

/// Extract the identifier carried by a recursive C++ declarator.
let private declaratorName (node: Node) =
    tryFindDescendant
        (fun child ->
            nodeType child = NodeType "identifier"
            || nodeType child = NodeType "field_identifier")
        node
    |> Option.map nodeText

/// Classify a C++ lambda directly initialized at module, class, or local scope.
let private anonymousBinding (node: Node) : CallableRole * string option =
    match nodeParent node with
    | Some field when
        nodeType field = NodeType "field_declaration"
        && (nodeParent field
            |> Option.exists (fun parent -> nodeType parent = NodeType "field_declaration_list"))
        ->
        BoundAnonymous ClassMemberBinding, (nodeField "declarator" field |> Option.bind declaratorName)
    | Some declarator when
        nodeType declarator = NodeType "init_declarator"
        && (nodeField "value" declarator
            |> Option.exists (fun value -> nodeId value = nodeId node))
        ->
        let bindingName = nodeField "declarator" declarator |> Option.bind declaratorName

        let role =
            match nodeParent declarator with
            | Some declaration when
                nodeType declaration = NodeType "declaration"
                && (nodeParent declaration
                    |> Option.exists (fun parent -> nodeType parent = NodeType "translation_unit"))
                ->
                BoundAnonymous ModuleBinding
            | Some field when
                nodeType field = NodeType "field_declaration"
                && (nodeParent field
                    |> Option.exists (fun parent -> nodeType parent = NodeType "field_declaration_list"))
                ->
                BoundAnonymous ClassMemberBinding
            | _ -> InlineAnonymous

        role, bindingName
    | _ -> InlineAnonymous, None

/// Normalize C++ functions and lambdas into shared callable views.
let private callableViews (node: Node) : CallableView list =
    match nodeType node with
    | nodeType when nodeType = functionDefinitionNodeType ->
        [ { Anchor = node
            Body = nodeField "body" node |> Option.defaultValue node
            Role = NamedDefinition
            BindingName = nodeField "declarator" node |> Option.bind declaratorName
            Parameters = parametersOf node } ]
    | nodeType when nodeType = lambdaExpressionNodeType ->
        let role, bindingName = anonymousBinding node

        [ { Anchor = node
            Body = nodeField "body" node |> Option.defaultValue node
            Role = role
            BindingName = bindingName
            Parameters = parametersOf node } ]
    | _ -> []

let private extractTypedParameter (node: Node) : TypedParameter option =
    let typeNode = nodeChildren node |> List.tryFind isTypeNode
    let declarator = nodeChildren node |> List.tryFind isDeclaratorNode

    match typeNode, declarator with
    | Some declaredType, Some declaration ->
        let nameNode =
            tryFindDescendant
                (fun candidate ->
                    nodeType candidate = NodeType "identifier"
                    || nodeType candidate = NodeType "field_identifier")
                declaration

        match nameNode with
        | Some name ->
            // decision: retain pointer/reference/array/function-declarator punctuation in the type
            // identity so `int` and `int*` are not treated as interchangeable primitive parameters.
            let declarationText = nodeText declaration

            let shape =
                declarationText.Replace((nodeText name), "")
                |> fun text -> Regex.Replace(text, "\\s+", "")

            Some
                { Name = nodeText name
                  Type = nodeText declaredType + shape }
        | None -> None
    | _ -> None

let private extractReturnType (node: Node) : string option =
    let trailing =
        nodeChildren node
        |> List.tryFind isDeclaratorNode
        |> Option.bind (tryFindDescendant (fun candidate -> nodeType candidate = NodeType "trailing_return_type"))

    match trailing with
    | Some trailingType -> tryFindDescendant isTypeNode trailingType |> Option.map nodeText
    | None -> nodeChildren node |> List.tryFind isTypeNode |> Option.map nodeText

let private switchBranchCount (node: Node) : int option =
    if nodeType node <> NodeType "switch_statement" then
        None
    else
        let cases =
            nodeNamedChildren node
            |> List.collect nodeNamedChildren
            |> List.filter (fun child -> nodeType child = NodeType "case_statement")

        let hasDefault =
            cases
            |> List.exists (fun caseNode ->
                nodeChildren caseNode
                |> List.exists (fun child -> nodeType child = NodeType "default"))

        Some(cases.Length + if hasDefault then 0 else 1)

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

let rec private isDefaultParameterValue (node: Node) : bool =
    match nodeParent node with
    | Some parent when nodeType parent = NodeType "optional_parameter_declaration" ->
        nodeChildren parent
        |> List.tryFind (fun child -> nodeType child = NodeType "=")
        |> Option.exists (fun equals -> nodeStartIndex node > nodeStartIndex equals)
    | Some parent when nodeType parent <> NodeType "function_definition" -> isDefaultParameterValue parent
    | _ -> false

let private isExplicitConstant (node: Node) : bool =
    if nodeType node = NodeType "enumerator" then
        true
    elif
        nodeType node = NodeType "declaration"
        || nodeType node = NodeType "field_declaration"
    then
        nodeChildren node
        |> List.exists (fun child ->
            nodeType child = NodeType "type_qualifier"
            && (nodeText child = "const" || nodeText child = "constexpr"))
    else
        false

let private baseClassNames (node: Node) : string list =
    match
        nodeChildren node
        |> List.tryFind (fun child -> nodeType child = NodeType "base_class_clause")
    with
    | Some clause -> nodeNamedChildren clause |> List.filter isTypeNode |> List.map nodeText
    | None -> []

/// Decide whether a literal can serve as a match/switch case label in C++.
///
/// decision: tree-sitter-cpp uses number_literal for both integral and floating literals; lexical
/// float markers are sufficient here because the parser has already validated the token. Hexadecimal
/// integers may contain `e`, so only `p` is an exponent marker after a 0x prefix.
/// float markers are sufficient here because the parser has already validated the token. Hexadecimal
/// integers may contain `e`, so only `p` is an exponent marker after a 0x prefix.
let private isMatchCaseLiteral (node: Node) : bool =
    if nodeType node = NodeType "char_literal" then
        true
    elif nodeType node <> NodeType "number_literal" then
        false
    else
        let text = (nodeText node).ToLowerInvariant()

        if text.StartsWith("0x", StringComparison.Ordinal) then
            not (text.Contains('.') || text.Contains('p'))
        else
            not (text.Contains('.') || text.Contains('e'))

let private isClassDefinition (node: Node) : bool =
    (nodeType node = NodeType "class_specifier"
     || nodeType node = NodeType "struct_specifier")
    && (nodeChildren node
        |> List.exists (fun child -> nodeType child = NodeType "field_declaration_list"))

/// Map C++ grammar constructs to the shared detector contracts.
let cPlusPlusLanguageAdapter: LanguageAdapter =
    { Id = "cpp"
      GrammarPath = "grammars/tree-sitter-cpp.wasm"
      NodeTypes =
        { Block = Some(NodeType "compound_statement")
          Parameters = parameterListNodeType
          IfStatement = Some(NodeType "if_statement")
          ElseClause = Some(NodeType "else_clause")
          ForStatement = Some(NodeType "for_statement")
          WhileStatement = Some(NodeType "while_statement")
          ConditionalExpression = Some(NodeType "conditional_expression")
          Lambda = Some lambdaExpressionNodeType
          ImportStatement = Some(NodeType "preproc_include")
          ImportFromStatement = None
          ExpressionStatement = Some(NodeType "expression_statement")
          Assignment = Some(NodeType "declaration")
          Module = Some(NodeType "translation_unit")
          ExportStatement = None
          Comment = Some(NodeType "comment")
          IntegerLiteral = Some(NodeType "number_literal")
          FloatLiteral = None
          StringLiteral = Some(NodeType "string_literal") }
      IsFunctionDefinition = fun node -> nodeType node = functionDefinitionNodeType
      // C++ has no merged-binding shape: one function_definition is one function.
      GetFunctionHeads = fun node -> [ { ParametersRoot = node; Body = node } ]
      GetCallableViews = callableViews
      IsStaticMethod = fun node -> nodeChildren node |> List.exists (fun child -> nodeText child = "static")
      ParameterChildTypes = parameterChildTypes
      DecisionNodeTypes =
        [ NodeType "if_statement"
          NodeType "for_statement"
          NodeType "for_range_loop"
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
              NodeType "for_range_loop"
              NodeType "while_statement"
              NodeType "do_statement"
              NodeType "catch_clause"
              NodeType "switch_statement" ]
      NestingControlTypes =
        [ NodeType "if_statement"
          NodeType "for_statement"
          NodeType "for_range_loop"
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
                    | NodeType "&&"
                    | NodeType "and" -> Some And
                    | NodeType "||"
                    | NodeType "or" -> Some Or
                    | _ -> None)
      IsTryElseClause = fun _ -> false
      VariableReferenceNodeTypes =
        [ NodeType "identifier"
          NodeType "field_expression"
          NodeType "qualified_identifier" ]
      ExtractTypedParameter = extractTypedParameter
      ExtractReturnType = extractReturnType
      GenericBrackets = { Open = "<"; Close = ">" }
      PrimitiveTypeNames =
        Set.ofList
            [ "bool"
              "char"
              "char8_t"
              "char16_t"
              "char32_t"
              "double"
              "float"
              "int"
              "long"
              "long double"
              "long int"
              "long long"
              "long long int"
              "short"
              "short int"
              "signed"
              "signed char"
              "signed int"
              "signed long"
              "signed long int"
              "signed long long"
              "signed long long int"
              "signed short"
              "signed short int"
              "string"
              "std::string"
              "unsigned"
              "unsigned char"
              "unsigned int"
              "unsigned long"
              "unsigned long int"
              "unsigned long long"
              "unsigned long long int"
              "unsigned short"
              "unsigned short int"
              "wchar_t" ]
      KeywordOnlyBoundaryTypes = []
      DistinctTypeAdvice = "a small value type (for example, a struct or enum class)"
      GetEqualityComparisons = equalityComparisons
      // C++'s switch-on-string is rare (switch requires integer/enum) and a documented gap shared with
      // Python/TS/Kotlin — only F#'s `match` gets the dedicated string-case hook.
      GetMatchStringCases = fun _ -> None
      GetMembershipComparisons = fun _ -> []
      IsMatchCaseLiteral = isMatchCaseLiteral
      GetElseIfBranches = fun _ -> []
      SubscriptNodeTypes = [ NodeType "subscript_expression" ]
      // Prefixed literals (u8"...", L"...", etc.) carry encoding semantics; raw strings use a
      // distinct raw_string_literal node and therefore never enter the bare-string detector.
      IsFormattedOrInterpolatedString =
        fun node -> not ((nodeText node).StartsWith("\"", System.StringComparison.Ordinal))
      IsDefaultParameterValue = isDefaultParameterValue
      IsBooleanLiteral = fun node -> nodeType node = NodeType "true" || nodeType node = NodeType "false"
      IsPositionalCallArgument =
        fun node ->
            match nodeParent node with
            | Some arguments when nodeType arguments = NodeType "argument_list" ->
                nodeParent arguments
                |> Option.exists (fun parent -> nodeType parent = NodeType "call_expression")
            | _ -> false
      IsExplicitConstant = isExplicitConstant
      ImportInfo =
        fun node ->
            let source =
                match
                    nodeChildren node
                    |> List.tryFind (fun child ->
                        nodeType child = NodeType "system_lib_string"
                        || nodeType child = NodeType "string_literal"
                        || nodeType child = NodeType "identifier")
                with
                | Some path -> (nodeText path).Trim([| '<'; '>'; '\"' |])
                | None -> nodeText node

            [ { Kind = Header
                Source = source
                Bindings = [] } ]
      IsClassDefinition = isClassDefinition
      GetClassName =
        fun node ->
            nodeChildren node
            |> List.tryFind (fun child ->
                nodeType child = NodeType "type_identifier"
                || nodeType child = NodeType "qualified_identifier")
            |> Option.map nodeText
      GetBaseClassNames = baseClassNames
      GetErrorHandlingRegion = errorHandlingRegion
      GetFunctionLogicalItems = bodyItems
      GetGuardedValidation =
        ValidationSyntax.extract
            { Containers = [ NodeType "compound_statement" ]
              Conditional = NodeType "if_statement"
              Rejections = [ NodeType "throw_statement" ]
              Return = Some(NodeType "return_statement")
              FailureCalls = []
              EmptyValues = []
              IsNonExecutable = fun _ -> false
              PreservesCheckedInformation = fun _ -> false } }
