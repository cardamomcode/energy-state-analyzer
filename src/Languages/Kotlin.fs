module Energy.Languages.Kotlin

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

let private tryExpressionNodeType = NodeType "try_expression"
let private blockNodeType = NodeType "block"
let private functionBodyNodeType = NodeType "function_body"
let private catchBlockNodeType = NodeType "catch_block"
let private finallyBlockNodeType = NodeType "finally_block"

/// Name Kotlin's function-declaration node type.
let private functionDeclarationNodeType = NodeType "function_declaration"

/// Name Kotlin's lambda-literal node type.
let private lambdaLiteralNodeType = NodeType "lambda_literal"

/// Name Kotlin's named-function parameter container.
let private functionParametersNodeType = NodeType "function_value_parameters"

/// Keep the existing Kotlin named-parameter contract in one reusable list.
let private namedParameterChildTypes = [ NodeType "parameter" ]

/// Extract named-function parameters from their Kotlin container.
let private namedParametersOf (node: Node) =
    nodeChildren node
    |> List.tryFind (fun child -> nodeType child = functionParametersNodeType)
    |> Option.map (fun parameters ->
        nodeChildren parameters
        |> List.filter (fun child -> namedParameterChildTypes |> List.contains (nodeType child)))
    |> Option.defaultValue []

/// Extract explicit lambda parameters while leaving implicit `it` uncounted.
let private lambdaParametersOf (node: Node) =
    nodeChildren node
    |> List.tryFind (fun child -> nodeType child = NodeType "lambda_parameters")
    |> Option.map (fun parameters ->
        nodeChildren parameters
        |> List.filter (fun child -> nodeType child = NodeType "variable_declaration"))
    |> Option.defaultValue []

/// Detect a top-level `contract { … }` call in a function body — Kotlin's construct for
/// carrying narrowing (for example nullability) across the call site.
///
/// decision: identifies the call by its leftmost callee name `contract`; the contract block is
/// a statement, so without this exemption a contracted validator would rely on the contract
/// incidentally breaking the guard-and-return shape instead of being intentionally excluded.
let private hasNarrowingContract (definition: Node) : bool =
    let isContractCall item =
        nodeType item = NodeType "call_expression"
        && (nodeNamedChildren item
            |> List.tryHead
            |> Option.exists (fun callee -> nodeType callee = NodeType "identifier" && nodeText callee = "contract"))

    nodeNamedChildren definition
    |> List.tryFind (fun child -> nodeType child = functionBodyNodeType)
    |> Option.bind (fun body -> nodeNamedChildren body |> List.tryHead)
    |> Option.bind (fun block -> nodeNamedChildren block |> List.tryFind isContractCall)
    |> Option.isSome

/// Read the polarity of Kotlin's true/false identifier tokens; None for any other node.
///
/// decision: shared by IsBooleanLiteral and BooleanLiteralValue so each literal text is compared
/// once per file — a second comparison would trip the magic-string detector on our own source.
let private booleanLiteralPolarity (node: Node) : BooleanLiteralPolarity option =
    if nodeType node <> NodeType "identifier" then
        None
    else
        match nodeText node with
        | "true" -> Some LiteralTrue
        | "false" -> Some LiteralFalse
        | _ -> None

/// Extract a Kotlin property's direct variable name.
let private propertyBindingName (property: Node) =
    nodeChildren property
    |> List.tryFind (fun child -> nodeType child = NodeType "variable_declaration")
    |> Option.bind (fun declaration ->
        nodeChildren declaration
        |> List.tryFind (fun child -> nodeType child = NodeType "identifier"))
    |> Option.map nodeText

/// Classify a Kotlin lambda directly assigned to a property.
let private anonymousBinding (node: Node) : CallableRole * string option =
    match nodeParent node with
    | Some property when
        nodeType property = NodeType "property_declaration"
        && (nodeNamedChildren property
            |> List.tryLast
            |> Option.exists (fun value -> nodeId value = nodeId node))
        ->
        let role =
            match nodeParent property with
            | Some parent when nodeType parent = NodeType "source_file" -> BoundAnonymous ModuleBinding
            | Some parent when nodeType parent = NodeType "class_body" -> BoundAnonymous ClassMemberBinding
            | _ -> InlineAnonymous

        role, propertyBindingName property
    | _ -> InlineAnonymous, None

/// Normalize Kotlin function declarations and lambda literals into callable views.
let private callableViews (node: Node) : CallableView list =
    match nodeType node with
    | nodeType when nodeType = functionDeclarationNodeType ->
        [ { Anchor = node
            Body = node
            ReturnTypeRoot = node
            Role = NamedDefinition
            BindingName = nodeField "name" node |> Option.map nodeText
            Parameters = namedParametersOf node } ]
    | nodeType when nodeType = lambdaLiteralNodeType ->
        let role, bindingName = anonymousBinding node

        [ { Anchor = node
            Body = node
            ReturnTypeRoot = node
            Role = role
            BindingName = bindingName
            Parameters = lambdaParametersOf node } ]
    | _ -> []

let rec private bodyItems (node: Node) : Node list =
    let children = nodeNamedChildren node

    match children |> List.tryFind (fun child -> nodeType child = blockNodeType) with
    | Some block -> nodeNamedChildren block
    | None ->
        match children |> List.tryFind (fun child -> nodeType child = functionBodyNodeType) with
        | Some body -> bodyItems body
        | None -> children

/// Preserve protected work, combined recovery work, and individual handler or cleanup bodies.
let private errorHandlingRegion (node: Node) : ErrorHandlingRegion option =
    if nodeType node <> tryExpressionNodeType then
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
                    nodeType child = catchBlockNodeType || nodeType child = finallyBlockNodeType)
                |> List.collect bodyItems
              RecoveryBodies =
                children
                |> List.filter (fun child ->
                    nodeType child = catchBlockNodeType || nodeType child = finallyBlockNodeType)
                |> List.map (fun clause ->
                    { Anchor = clause
                      Items = bodyItems clause }) })

/// Fix the expected identifier count of an infix expression node.
///
/// decision: an infix expression is flagged only when it has exactly three identifier children
/// (operand operator operand); this is a property of that shape, not a tunable threshold, so it
/// stays as a named constant at the top of the module rather than in Core.Config.
let private expectedInfixIdentifierCount = 3

// The Kotlin LanguageAdapter.
//
// tree-sitter-kotlin (v1.1.0) has a real block wrapper like Python/TS, but its `else` is a bare
// keyword token with no wrapper node at all: an `else if` chain's next `if_expression` is a direct
// child of the previous one, not nested inside an else_clause (TS) nor a flat elif sibling (Python/
// F#). This adapter alone (elseClause: null, getElseIfBranches: []) is not sufficient for the
// match-opportunity and inversion detectors, which handle that third shape themselves — but it is
// correct on its own for the hooks below. Every hook operates on a raw `Node` through the TreeSitter
// typed accessors; `.children` is an always-present list, read directly like Python's port.

/// Detect a user_type node, factored out to avoid stringly-typed branch checks.
///
/// decision: split out of getBaseClassNames/getTypedParameter into their own function, rather than
/// several `c.type === '...'` comparisons inline — that shape is exactly what the primitive-obsession
/// detector's stringly-typed-control-flow check flags as a switch-like branch on an ad hoc string tag.
let private isUserType (node: Node) : bool = nodeType node = NodeType "user_type"

/// Detect a parameter's type annotation, accepting both plain and nullable forms.
///
/// decision: a nullable parameter (`String?`) wraps its user_type in a nullable_type node, so
/// matching only user_type made nullable parameters invisible to typed-parameter consumers — the
/// identity-return check in particular could never agree a `String?` parameter with a `String?`
/// return, while the C#/C++/TS equivalents were flagged (review inconsistency). Matching either
/// form keeps the exact type text (the `?` included): `String?` agrees with `String?`, never
/// with `String`.
let private isParameterType (node: Node) : bool =
    nodeType node = NodeType "user_type" || nodeType node = NodeType "nullable_type"

let private isConstPropertyDeclaration (node: Node) : bool =
    match nodeChildren node |> List.tryFind (fun c -> nodeType c = NodeType "modifiers") with
    | Some modifiers ->
        nodeChildren modifiers
        |> List.exists (fun modifier ->
            nodeType modifier = NodeType "property_modifier"
            && (nodeChildren modifier |> List.exists (fun c -> nodeType c = NodeType "const")))
    | None -> false

/// Recognize the annotated-const-val misparse so an annotated const val isn't wrongly flagged as magic.
///
/// decision: a leading annotation (`@VisibleForTesting const val X = 5`) makes this grammar lose the
/// property_declaration/modifiers shape entirely and instead parse the whole line as a generic
/// `assignment` whose LHS is an `annotated_expression` wrapping an `infix_expression` with `const`/
/// `val`/the name as three bare identifier tokens (verified by dumping the parse tree) — recognize
/// that specific misparse shape so an annotated const val isn't wrongly flagged as magic.
let private isAnnotatedConstValMisparse (node: Node) : bool =
    match
        nodeChildren node
        |> List.tryFind (fun c -> nodeType c = NodeType "annotated_expression")
    with
    | Some annotated ->
        match
            nodeChildren annotated
            |> List.tryFind (fun c -> nodeType c = NodeType "infix_expression")
        with
        | Some infix ->
            let identifiers =
                nodeChildren infix |> List.filter (fun c -> nodeType c = NodeType "identifier")

            if List.length identifiers <> expectedInfixIdentifierCount then
                false
            else
                let ids = List.toArray identifiers

                nodeText ids.[0] = "const" && nodeText ids.[1] = "val"
        | None -> false
    | None -> false

/// Extract a single delegation specifier's target name.
///
/// decision: split out of getBaseClassNames into its own function, rather than several `c.type ===
/// '...'` comparisons inline — same rationale as isUserType above.
let private delegationSpecifierName (specifier: Node) : string option =
    nodeChildren specifier
    |> List.tryFind isUserType
    |> Option.orElse (
        nodeChildren specifier
        |> List.tryFind (fun c -> nodeType c = NodeType "constructor_invocation")
        |> Option.bind (fun ci -> nodeChildren ci |> List.tryFind isUserType)
    )
    |> Option.bind (fun ut ->
        nodeChildren ut
        |> List.tryFind (fun c -> nodeType c = NodeType "identifier")
        |> Option.map nodeText)

/// Treat a compiler-enforced const val as a compile-time constant at any nesting depth.
///
/// decision: `const val` is an explicit, compiler-enforced compile-time-constant marker — unlike the
/// module-scope heuristic isInConstantContext (magicNumber.ts) otherwise relies on, this is valid at
/// ANY nesting depth (a companion object's `const val` is just as much a real constant as a top-level
/// one), so it's checked as its own signal rather than folded into that scope walk.
let kotlinLanguageAdapter: LanguageAdapter =
    { Id = "kotlin"
      GrammarPath = "grammars/tree-sitter-kotlin.wasm"
      NodeTypes =
        { Block = Some(NodeType "block")
          Parameters = functionParametersNodeType
          IfStatement = Some(NodeType "if_expression")
          ElseClause = None
          ForStatement = Some(NodeType "for_statement")
          WhileStatement = Some(NodeType "while_statement")
          // if_expression already covers ternary-style use (Kotlin has no separate ternary node).
          ConditionalExpression = None
          Lambda = Some lambdaLiteralNodeType
          ImportStatement = Some(NodeType "import")
          ImportFromStatement = None
          ExpressionStatement = None
          // 'property_declaration' (val/var NAME = value), not 'assignment' (bare reassignment `x = 5`)
          // — the only consumer (magicNumber.ts's isInConstantContext) wants "is this literal the value
          // of a named declaration", which is what Python's `assignment`/TS's `lexical_declaration` mean there too.
          Assignment = Some(NodeType "property_declaration")
          Module = Some(NodeType "source_file")
          ExportStatement = None
          // grammar splits line_comment/block_comment; this single-string field can only name one —
          // block comments are a minor documented gap (inversion.ts's statement filter is the only
          // consumer, and only for a comment as literally the first line).
          Comment = Some(NodeType "line_comment")
          IntegerLiteral = Some(NodeType "number_literal")
          FloatLiteral = Some(NodeType "float_literal")
          StringLiteral = Some(NodeType "string_literal") }
      IsFunctionDefinition = fun node -> nodeType node = functionDeclarationNodeType
      // Kotlin has no merged-binding shape: one definition node is one function.
      GetFunctionHeads = fun node -> [ { ParametersRoot = node; Body = node } ]
      GetCallableViews = callableViews
      IsStaticMethod = fun _ -> false
      ParameterChildTypes = namedParameterChildTypes
      DecisionNodeTypes =
        [ NodeType "if_expression"
          NodeType "for_statement"
          NodeType "while_statement"
          NodeType "when_expression"
          NodeType "catch_block" ]
      CyclomaticBranchCount =
        fun node ->
            if nodeType node <> NodeType "when_expression" then
                None
            else
                let entries =
                    nodeNamedChildren node
                    |> List.filter (fun child -> nodeType child = NodeType "when_entry")

                let hasFallback =
                    entries |> List.exists (fun entry -> nodeText entry |> _.Contains("else ->"))

                Some(entries.Length + if hasFallback then 0 else 1)
      GetCognitiveStructure =
        CognitiveSyntax.classify
            [ NodeType "if_expression"
              NodeType "for_statement"
              NodeType "while_statement"
              NodeType "when_expression"
              NodeType "catch_block"
              NodeType "do_while_statement" ]
      NestingControlTypes =
        [ NodeType "if_expression"
          NodeType "for_statement"
          NodeType "while_statement"
          NodeType "try_expression" ]
      GetBooleanOperator =
        fun node ->
            if nodeType node <> NodeType "binary_expression" then
                None
            else
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = NodeType "&&" || nodeType c = NodeType "||")
                |> Option.map (fun c -> if nodeType c = NodeType "&&" then And else Or)
      // Kotlin's try/catch has no else-branch construct.
      IsTryElseClause = fun _ -> false
      VariableReferenceNodeTypes = [ NodeType "identifier"; NodeType "navigation_expression" ]
      ExtractTypedParameter =
        fun node ->
            if nodeType node <> NodeType "parameter" then
                None
            else
                match
                    nodeChildren node |> List.tryFind (fun c -> nodeType c = NodeType "identifier"),
                    nodeChildren node |> List.tryFind isParameterType
                with
                | Some nameNode, Some typeNode ->
                    // Preserve generic arguments and the nullable marker so a parameter and its
                    // return type agree exactly.
                    Some
                        { Name = nodeText nameNode
                          Type = nodeText typeNode }
                | _ -> None
      ExtractReturnType =
        fun node ->
            // decision: scans only the function node's own direct children (`:` followed by the
            // return-type node, after function_value_parameters and before function_body) — a parameter's
            // own `:` and type live one level deeper, inside function_value_parameters, so this can't
            // accidentally pick up a parameter's type instead of the return type.
            match nodeChildren node |> List.tryFindIndex (fun c -> nodeType c = NodeType ":") with
            | Some ci when ci + 1 < List.length (nodeChildren node) ->
                let children = nodeChildren node

                Some(nodeText (List.item (ci + 1) children))
            | _ -> None
      GenericBrackets = { Open = "<"; Close = ">" }
      PrimitiveTypeNames =
        Set.ofList
            [ "Int"
              "Long"
              "Short"
              "Byte"
              "Double"
              "Float"
              "Boolean"
              "String"
              "Char" ]
      // Kotlin has no enforced-keyword-only parameter syntax (named arguments are optional at the call
      // site) — see language.ts's field doc, same reasoning as F#.
      KeywordOnlyBoundaryTypes = []
      // decision: only suggests value class, not typealias — a typealias is just a synonym (the
      // compiler still sees the underlying primitive), so it wouldn't actually catch the swap this
      // warning is about, unlike Python's NewType/TS's branded type/F#'s single-case union, which this
      // field's other adapters correctly point to.
      DistinctTypeAdvice = "a value class (@JvmInline value class)"
      GetEqualityComparisons =
        fun node ->
            if nodeType node <> NodeType "binary_expression" then
                []
            else
                match
                    nodeChildren node
                    |> List.tryFind (fun c -> nodeType c = NodeType "==" || nodeType c = NodeType "===")
                with
                | Some opToken ->
                    // decision: compare operand identity by `.id`, not structural equality.
                    let operands =
                        nodeChildren node |> List.filter (fun c -> nodeId c <> nodeId opToken)

                    match operands with
                    | [ l; r ] -> [ { Left = l; Right = r } ]
                    | _ -> []
                | None -> []
      // Kotlin's when-on-string is a documented gap shared with Python/TS/C++ — only F#'s `match` gets
      // the dedicated string-case hook.
      GetMatchStringCases = fun _ -> None
      // Kotlin's set-membership idiom (`x in listOf(...)`) is an in_expression whose right side is
      // normally a call_expression, not a literal collection — not modeled here, same precedent as
      // typescript.ts. Repeated equality checks still accumulate via getEqualityComparisons.
      GetMembershipComparisons = fun _ -> []
      IsMatchCaseLiteral =
        fun node ->
            nodeType node = NodeType "string_literal"
            || nodeType node = NodeType "number_literal"
            || nodeType node = NodeType "float_literal"
      // No flat elif node exists — Kotlin's chain is walked via the bare-nested-if fallback in
      // matchOpportunity.ts's collectChainBranches instead.
      GetElseIfBranches = fun _ -> []
      SubscriptNodeTypes = [ NodeType "index_expression" ]
      IsFormattedOrInterpolatedString =
        fun node ->
            nodeChildren node
            |> List.exists (fun c -> nodeType c = NodeType "interpolation")
      IsDefaultParameterValue =
        fun node ->
            // decision: compares node identity by `.id`, not reference equality — see the matching
            // comment in python.ts's isFormattedOrInterpolatedString for why.
            // decision: a default value isn't nested inside the `parameter` node itself —
            // function_value_parameters is a flat seq(parameter_modifiers?, parameter, ('=' expr)?), so
            // the default value's siblings (not ancestors) are the '=' token and the parameter.
            match nodeParent node with
            | Some parent when nodeType parent = NodeType "function_value_parameters" ->
                let siblings = nodeChildren parent

                match List.tryFindIndex (fun c -> nodeId c = nodeId node) siblings with
                | Some index when index >= 2 ->
                    match List.tryItem (index - 1) siblings, List.tryItem (index - 2) siblings with
                    | Some prev, Some prevPrev ->
                        nodeType prev = NodeType "=" && nodeType prevPrev = NodeType "parameter"
                    | _ -> false
                | _ -> false
            | _ -> false
      // decision: true/false have no dedicated literal node in this grammar — they lex as plain
      // `identifier` tokens (verified: no boolean_literal rule exists). Safe to key off text since
      // true/false are hard keywords in Kotlin, not shadowable identifiers.
      IsBooleanLiteral = fun node -> Option.isSome (booleanLiteralPolarity node)
      // decision: every call argument (named or positional) wraps in `value_argument`, so unlike the
      // other adapters' direct-parent check, this also has to rule out a named argument (`retries =
      // true`) by checking the literal is value_argument's *first* child — a named argument's
      // value_argument instead starts with `identifier '='` before the value.
      IsPositionalCallArgument =
        fun node ->
            match nodeParent node with
            | Some valueArgument when nodeType valueArgument = NodeType "value_argument" ->
                match nodeParent valueArgument with
                | Some vaParents when nodeType vaParents = NodeType "value_arguments" ->
                    match nodeParent vaParents with
                    | Some callParent when nodeType callParent = NodeType "call_expression" ->
                        match List.tryItem 0 (nodeChildren valueArgument) with
                        | Some first -> nodeId first = nodeId node
                        | None -> false
                    | _ -> false
                | _ -> false
            | _ -> false
      IsExplicitConstant =
        fun node ->
            match nodeType node with
            | NodeType "property_declaration" -> isConstPropertyDeclaration node
            | NodeType "assignment" -> isAnnotatedConstValMisparse node
            | _ -> false
      // Kotlin imports one declaration into local scope; preserve that declaration separately from its
      // package so coherence can identify a wide local vocabulary from one API.
      ImportInfo =
        fun node ->
            match
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = NodeType "qualified_identifier")
            with
            | Some qualified when nodeText qualified <> "" ->
                let text = nodeText qualified

                if nodeChildren node |> List.exists (fun c -> nodeType c = NodeType "*") then
                    [ { Kind = Wildcard
                        Source = text
                        Bindings = [] } ]
                else
                    match text.LastIndexOf('.') with
                    | -1 ->
                        [ { Kind = Members
                            Source = text
                            Bindings = [] } ]
                    | idx ->
                        let name = text.Substring(idx + 1)

                        [ { Kind = Members
                            Source = text.Substring(0, idx)
                            Bindings =
                              [ { ImportedName = name
                                  LocalName = name } ] } ]
            | _ ->
                [ { Kind = Members
                    Source = nodeText node
                    Bindings = [] } ]
      IsClassDefinition = fun node -> nodeType node = NodeType "class_declaration"
      GetClassName =
        fun node ->
            nodeChildren node
            |> List.tryFind (fun c -> nodeType c = NodeType "identifier")
            |> Option.map nodeText
      // `class Foo : Bar(), Baz` -> ['Bar', 'Baz']. Each delegation_specifier wraps either a
      // constructor_invocation (a superclass call, `Bar()`) or a bare user_type (an interface, `Baz`)
      // — both nest their name one level deeper inside a user_type node.
      GetBaseClassNames =
        fun node ->
            match
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = NodeType "delegation_specifiers")
            with
            | Some specifiers ->
                nodeChildren specifiers
                |> List.filter (fun s -> nodeType s = NodeType "delegation_specifier")
                |> List.choose delegationSpecifierName
            | None -> []
      GetErrorHandlingRegion = errorHandlingRegion
      GetFunctionLogicalItems = bodyItems
      GetGuardedValidation =
        ValidationSyntax.extract
            { Containers = [ NodeType "function_body"; NodeType "block" ]
              Conditional = NodeType "if_expression"
              Rejections = [ NodeType "throw_expression" ]
              Return = Some(NodeType "return_expression")
              FailureCalls = []
              EmptyValues = [ "Unit" ]
              IsNonExecutable = fun _ -> false
              BooleanLiteralValue = booleanLiteralPolarity
              // decision: a `contract { … }` block carries the narrowing across the call site, so a
              // contracted boolean validator is not a plain boolean leak.
              PreservesCheckedInformation = hasNarrowingContract } }
