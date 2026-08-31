module Energy.Languages.CPlusPlus

open System
open System.Text.RegularExpressions

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// The C++ LanguageAdapter. Grammar node names and shapes below target the official
// tree-sitter-cpp v0.23.4 WASM bundled in grammars/; its checksum and license are recorded beside
// the artifact. C++ declarators are recursive, so parameter extraction deliberately separates the
// direct type specifier from the declarator shape instead of assuming a flat `type name` pair.

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

// decision: tree-sitter-cpp uses number_literal for both integral and floating literals; lexical
// float markers are sufficient here because the parser has already validated the token. Hexadecimal
// integers may contain `e`, so only `p` is an exponent marker after a 0x prefix.
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

let CPP: LanguageAdapter =
    { Id = "cpp"
      GrammarPath = "grammars/tree-sitter-cpp.wasm"
      NodeTypes =
        { Block = Some(NodeType "compound_statement")
          Parameters = NodeType "parameter_list"
          IfStatement = Some(NodeType "if_statement")
          ElseClause = Some(NodeType "else_clause")
          ForStatement = Some(NodeType "for_statement")
          WhileStatement = Some(NodeType "while_statement")
          ConditionalExpression = Some(NodeType "conditional_expression")
          Lambda = Some(NodeType "lambda_expression")
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
      IsFunctionDefinition = fun node -> nodeType node = NodeType "function_definition"
      ParameterChildTypes =
        [ NodeType "parameter_declaration"
          NodeType "optional_parameter_declaration"
          NodeType "variadic_parameter_declaration" ]
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
      CognitiveNestedDecisionTypes =
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
      EntersNestedScope =
        fun node ->
            match nodeType node with
            | NodeType kind ->
                kind = "compound_statement"
                || kind = "for_range_loop"
                || kind.EndsWith("_statement", StringComparison.Ordinal)
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
      GetMembershipComparisons = fun _ -> []
      IsMatchCaseLiteral = isMatchCaseLiteral
      GetElseIfBranches = fun _ -> []
      SubscriptNodeTypes = [ NodeType "subscript_expression" ]
      // Prefixed literals (u8"...", L"...", etc.) carry encoding semantics; raw strings use a
      // distinct raw_string_literal node and therefore never enter the bare-string detector.
      IsFormattedOrInterpolatedString = fun node -> not ((nodeText node).StartsWith("\""))
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
      ImportSource =
        fun node ->
            match
                nodeChildren node
                |> List.tryFind (fun child ->
                    nodeType child = NodeType "system_lib_string"
                    || nodeType child = NodeType "string_literal"
                    || nodeType child = NodeType "identifier")
            with
            | Some path -> (nodeText path).Trim([| '<'; '>'; '\"' |])
            | None -> nodeText node
      IsClassDefinition = isClassDefinition
      GetClassName =
        fun node ->
            nodeChildren node
            |> List.tryFind (fun child ->
                nodeType child = NodeType "type_identifier"
                || nodeType child = NodeType "qualified_identifier")
            |> Option.map nodeText
      GetBaseClassNames = baseClassNames }
