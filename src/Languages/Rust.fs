module Energy.Languages.Rust

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

let private kind name = NodeType name
let private is kindName node = nodeType node = kind kindName

/// Read direct parameters from a Rust function or closure.
let private parameters node =
    nodeField "parameters" node
    |> Option.map (fun list ->
        let children = nodeNamedChildren list

        if is "closure_parameters" list then
            children
        else
            children |> List.filter (is "parameter"))
    |> Option.defaultValue []

/// Identify receiver syntax without counting it as a caller-supplied argument.
let private hasSelfParameter node =
    nodeField "parameters" node
    |> Option.exists (fun list -> nodeNamedChildren list |> List.exists (is "self_parameter"))

/// Keep an associated method out of the module-function count while retaining its body metrics.
let private callableViews node =
    match nodeType node with
    | NodeType "function_item" ->
        [
            {
                Anchor = node
                Body = nodeField "body" node |> Option.defaultValue node
                ReturnTypeRoot = node
                Role = NamedDefinition
                BindingName = nodeField "name" node |> Option.map nodeText
                Parameters = parameters node
            }
        ]
    | NodeType "closure_expression" ->
        let role, name =
            match nodeParent node with
            | Some binding when is "let_declaration" binding ->
                let name = nodeField "pattern" binding |> Option.map nodeText

                let scope =
                    nodeParent binding
                    |> Option.map nodeType
                    |> function
                        | Some(NodeType "source_file") -> BoundAnonymous ModuleBinding
                        | _ -> InlineAnonymous

                scope, name
            | _ -> InlineAnonymous, None

        [
            {
                Anchor = node
                Body = nodeField "body" node |> Option.defaultValue node
                ReturnTypeRoot = node
                Role = role
                BindingName = name
                Parameters = parameters node
            }
        ]
    | _ -> []

/// Extract the explicit type and simple binding name from a Rust parameter.
let private typedParameter node =
    match nodeField "pattern" node, nodeField "type" node with
    | Some name, Some declaredType when is "identifier" name ->
        Some
            {
                Name = nodeText name
                Type = nodeText declaredType
            }
    | _ -> None

/// Count the arms of a Rust match as its explicit alternatives.
///
/// invariant: valid Rust matches are exhaustive, so no implicit unmatched path is added.
let private branchCount node =
    if not (is "match_expression" node) then
        None
    else
        let arms =
            nodeField "body" node
            |> Option.map nodeNamedChildren
            |> Option.defaultValue []
            |> List.filter (is "match_arm")

        Some arms.Length

/// Keep match arm bodies nested under their dispatch for cognitive scoring.
let private cognitiveStructure node =
    if is "match_expression" node then
        let arms =
            nodeField "body" node
            |> Option.map nodeNamedChildren
            |> Option.defaultValue []
            |> List.filter (is "match_arm")

        Some(Flow(Structural, arms))
    else
        CognitiveSyntax.classify
            [
                kind "if_expression"
                kind "for_expression"
                kind "while_expression"
                kind "loop_expression"
            ]
            node

/// Adapt the official tree-sitter-rust v0.24.2 grammar to the shared pipeline.
let rustLanguageAdapter: LanguageAdapter =
    {
        Id = "rust"
        GrammarPath = "grammars/tree-sitter-rust.wasm"
        NodeTypes =
            {
                Block = Some(kind "block")
                Parameters = kind "parameters"
                IfStatement = Some(kind "if_expression")
                ElseClause = Some(kind "else_clause")
                ForStatement = Some(kind "for_expression")
                WhileStatement = Some(kind "while_expression")
                ConditionalExpression = None
                Lambda = Some(kind "closure_expression")
                ImportStatement = Some(kind "use_declaration")
                ImportFromStatement = None
                AdditionalImportTypes = []
                ExpressionStatement = Some(kind "expression_statement")
                Assignment = Some(kind "let_declaration")
                Module = Some(kind "source_file")
                ExportStatement = None
                Comment = Some(kind "line_comment")
                IntegerLiteral = Some(kind "integer_literal")
                FloatLiteral = Some(kind "float_literal")
                StringLiteral = Some(kind "string_literal")
            }
        IsFunctionDefinition = is "function_item"
        GetFunctionHeads =
            fun node ->
                [
                    {
                        ParametersRoot = node
                        Body = nodeField "body" node |> Option.defaultValue node
                    }
                ]
        GetCallableViews = callableViews
        ParameterChildTypes = [ kind "parameter" ]
        DecisionNodeTypes =
            [
                kind "if_expression"
                kind "for_expression"
                kind "while_expression"
                kind "loop_expression"
                kind "match_expression"
            ]
        CyclomaticBranchCount = branchCount
        GetCognitiveStructure = cognitiveStructure
        NestingControlTypes =
            [
                kind "if_expression"
                kind "for_expression"
                kind "while_expression"
                kind "loop_expression"
                kind "match_expression"
            ]
        GetBooleanOperator =
            fun node ->
                if not (is "binary_expression" node) then
                    None
                else
                    match nodeField "operator" node |> Option.map nodeText with
                    | Some "&&" -> Some And
                    | Some "||" -> Some Or
                    | _ -> None
        IsTryElseClause = fun _ -> false
        VariableReferenceNodeTypes = [ kind "identifier"; kind "field_expression"; kind "scoped_identifier" ]
        ExtractTypedParameter = typedParameter
        ExtractReturnType = fun node -> nodeField "return_type" node |> Option.map nodeText
        GenericBrackets = { Open = "<"; Close = ">" }
        PrimitiveTypeNames =
            Set.ofList
                [
                    "bool"
                    "char"
                    "i8"
                    "i16"
                    "i32"
                    "i64"
                    "i128"
                    "isize"
                    "u8"
                    "u16"
                    "u32"
                    "u64"
                    "u128"
                    "usize"
                    "f32"
                    "f64"
                    "String"
                ]
        KeywordOnlyBoundaryTypes = []
        DistinctTypeAdvice = "a newtype struct or enum"
        GetEqualityComparisons =
            fun node ->
                if
                    is "binary_expression" node
                    && (nodeField "operator" node |> Option.exists (fun op -> nodeText op = "=="))
                then
                    match nodeField "left" node, nodeField "right" node with
                    | Some left, Some right -> [ { Left = left; Right = right } ]
                    | _ -> []
                else
                    []
        GetMatchStringCases = fun _ -> None
        GetMembershipComparisons = fun _ -> []
        IsMatchCaseLiteral =
            fun node ->
                List.contains
                    (nodeType node)
                    [
                        kind "integer_literal"
                        kind "char_literal"
                        kind "boolean_literal"
                        kind "string_literal"
                    ]
        GetElseIfBranches = fun _ -> []
        SubscriptNodeTypes = [ kind "index_expression" ]
        IsFormattedOrInterpolatedString = fun _ -> false
        IsDefaultParameterValue = fun _ -> false
        IsStructuralNumber = fun node -> nodeParent node |> Option.exists (is "array_type")
        IsBooleanLiteral = is "boolean_literal"
        IsPositionalCallArgument =
            fun node ->
                nodeParent node
                |> Option.exists (fun parent ->
                    is "arguments" parent
                    && (nodeParent parent |> Option.exists (is "call_expression")))
        IsExplicitConstant =
            fun node -> List.contains (nodeType node) [ kind "const_item"; kind "static_item"; kind "enum_variant" ]
        ImportInfo = RustImports.importInfo
        // decision: an impl block owns its methods for coherence; Rust structs contain fields,
        // while methods live in impl blocks and must not inflate the free-function count.
        IsClassDefinition = is "impl_item"
        IsStaticMethod = hasSelfParameter >> not
        GetClassName = fun node -> nodeField "type" node |> Option.map nodeText
        GetBaseClassNames = fun _ -> []
        GetErrorHandlingRegion = fun _ -> None
        GetFunctionLogicalItems =
            fun node ->
                nodeField "body" node
                |> Option.map nodeNamedChildren
                |> Option.defaultValue (nodeNamedChildren node)
        GetGuardedValidation = fun _ -> None
    }
