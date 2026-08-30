module Energy.Languages.Kotlin

open Fable.Core
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// The Kotlin LanguageAdapter (port of src/languages/kotlin.ts).
//
// tree-sitter-kotlin (v1.1.0) has a real block wrapper like Python/TS, but its `else` is a bare
// keyword token with no wrapper node at all: an `else if` chain's next `if_expression` is a direct
// child of the previous one, not nested inside an else_clause (TS) nor a flat elif sibling (Python/
// F#). This adapter alone (elseClause: null, getElseIfBranches: []) is not sufficient for the
// match-opportunity and inversion detectors, which handle that third shape themselves — but it is
// correct on its own for the hooks below. Every hook operates on a raw `Node` through the TreeSitter
// typed accessors; `.children` is an always-present list, read directly like Python's port.

// decision: split out of getBaseClassNames/getTypedParameter into their own function, rather than
// several `c.type === '...'` comparisons inline — that shape is exactly what the primitive-obsession
// detector's stringly-typed-control-flow check flags as a switch-like branch on an ad hoc string tag.
let private isUserType (node: Node) : bool = nodeType node = "user_type"

let private isConstPropertyDeclaration (node: Node) : bool =
    match nodeChildren node |> List.tryFind (fun c -> nodeType c = "modifiers") with
    | Some modifiers ->
        nodeChildren modifiers
        |> List.exists (fun modifier ->
            nodeType modifier = "property_modifier"
            && (nodeChildren modifier |> List.exists (fun c -> nodeType c = "const")))
    | None -> false

// decision: a leading annotation (`@VisibleForTesting const val X = 5`) makes this grammar lose the
// property_declaration/modifiers shape entirely and instead parse the whole line as a generic
// `assignment` whose LHS is an `annotated_expression` wrapping an `infix_expression` with `const`/
// `val`/the name as three bare identifier tokens (verified by dumping the parse tree) — recognize
// that specific misparse shape so an annotated const val isn't wrongly flagged as magic.
let private isAnnotatedConstValMisparse (node: Node) : bool =
    match nodeChildren node |> List.tryFind (fun c -> nodeType c = "annotated_expression") with
    | Some annotated ->
        match
            nodeChildren annotated
            |> List.tryFind (fun c -> nodeType c = "infix_expression")
        with
        | Some infix ->
            let identifiers =
                nodeChildren infix |> List.filter (fun c -> nodeType c = "identifier")

            if List.length identifiers <> 3 then
                false
            else
                let ids = List.toArray identifiers

                nodeText ids.[0] = "const" && nodeText ids.[1] = "val"
        | None -> false
    | None -> false

// decision: split out of getBaseClassNames into its own function, rather than several `c.type ===
// '...'` comparisons inline — same rationale as isUserType above.
let private delegationSpecifierName (specifier: Node) : string option =
    let userType =
        match nodeChildren specifier |> List.tryFind isUserType with
        | Some ut -> Some ut
        | None ->
            match
                nodeChildren specifier
                |> List.tryFind (fun c -> nodeType c = "constructor_invocation")
            with
            | Some ci -> nodeChildren ci |> List.tryFind isUserType
            | None -> None

    match userType with
    | Some ut ->
        nodeChildren ut
        |> List.tryFind (fun c -> nodeType c = "identifier")
        |> Option.map nodeText
    | None -> None

// decision: `const val` is an explicit, compiler-enforced compile-time-constant marker — unlike the
// module-scope heuristic isInConstantContext (magicNumber.ts) otherwise relies on, this is valid at
// ANY nesting depth (a companion object's `const val` is just as much a real constant as a top-level
// one), so it's checked as its own signal rather than folded into that scope walk.
let KOTLIN: LanguageAdapter =
    { Id = "kotlin"
      GrammarPath = "grammars/tree-sitter-kotlin.wasm"
      NodeTypes =
        { Block = Some "block"
          Parameters = "function_value_parameters"
          IfStatement = Some "if_expression"
          ElseClause = None
          ForStatement = Some "for_statement"
          WhileStatement = Some "while_statement"
          // if_expression already covers ternary-style use (Kotlin has no separate ternary node).
          ConditionalExpression = None
          Lambda = Some "lambda_literal"
          ImportStatement = Some "import"
          ImportFromStatement = None
          ExpressionStatement = None
          // 'property_declaration' (val/var NAME = value), not 'assignment' (bare reassignment `x = 5`)
          // — the only consumer (magicNumber.ts's isInConstantContext) wants "is this literal the value
          // of a named declaration", which is what Python's `assignment`/TS's `lexical_declaration` mean there too.
          Assignment = Some "property_declaration"
          Module = Some "source_file"
          ExportStatement = None
          // grammar splits line_comment/block_comment; this single-string field can only name one —
          // block comments are a minor documented gap (inversion.ts's statement filter is the only
          // consumer, and only for a comment as literally the first line).
          Comment = Some "line_comment"
          IntegerLiteral = Some "number_literal"
          FloatLiteral = Some "float_literal"
          StringLiteral = Some "string_literal" }
      IsFunctionDefinition = fun node -> nodeType node = "function_declaration"
      ParameterChildTypes = [ "parameter" ]
      DecisionNodeTypes =
        [ "if_expression"
          "for_statement"
          "while_statement"
          "when_expression"
          "catch_block" ]
      CognitiveNestedDecisionTypes =
        [ "if_expression"
          "for_statement"
          "while_statement"
          "when_expression"
          "catch_block" ]
      NestingControlTypes = [ "if_expression"; "for_statement"; "while_statement"; "try_expression" ]
      GetBooleanOperator =
        fun node ->
            if nodeType node <> "binary_expression" then
                None
            else
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = "&&" || nodeType c = "||")
                |> Option.map (fun c -> if nodeType c = "&&" then And else Or)
      EntersNestedScope = fun node -> nodeType node = "block"
      // Kotlin's try/catch has no else-branch construct.
      IsTryElseClause = fun _ -> false
      VariableReferenceNodeTypes = [ "identifier"; "navigation_expression" ]
      ExtractTypedParameter =
        fun node ->
            if nodeType node <> "parameter" then
                None
            else
                match
                    nodeChildren node |> List.tryFind (fun c -> nodeType c = "identifier"),
                    nodeChildren node |> List.tryFind isUserType
                with
                | Some nameNode, Some typeNode ->
                    match nodeChildren typeNode |> List.tryFind (fun c -> nodeType c = "identifier") with
                    | Some ti ->
                        Some
                            { Name = nodeText nameNode
                              Type = nodeText ti }
                    | None -> None
                | _ -> None
      ExtractReturnType =
        fun node ->
            // decision: scans only the function node's own direct children (`:` followed by the
            // return-type node, after function_value_parameters and before function_body) — a parameter's
            // own `:` and type live one level deeper, inside function_value_parameters, so this can't
            // accidentally pick up a parameter's type instead of the return type.
            match nodeChildren node |> List.tryFindIndex (fun c -> nodeType c = ":") with
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
            if nodeType node <> "binary_expression" then
                []
            else
                match
                    nodeChildren node
                    |> List.tryFind (fun c -> nodeType c = "==" || nodeType c = "===")
                with
                | Some opToken ->
                    // decision: compare operand identity by `.id`, not structural equality.
                    let operands =
                        nodeChildren node |> List.filter (fun c -> nodeId c <> nodeId opToken)

                    match operands with
                    | [ l; r ] -> [ { Left = l; Right = r } ]
                    | _ -> []
                | None -> []
      // Kotlin's set-membership idiom (`x in listOf(...)`) is an in_expression whose right side is
      // normally a call_expression, not a literal collection — not modeled here, same precedent as
      // typescript.ts. Repeated equality checks still accumulate via getEqualityComparisons.
      GetMembershipComparisons = fun _ -> []
      // No flat elif node exists — Kotlin's chain is walked via the bare-nested-if fallback in
      // matchOpportunity.ts's collectChainBranches instead.
      GetElseIfBranches = fun _ -> []
      SubscriptNodeTypes = [ "index_expression" ]
      IsFormattedOrInterpolatedString =
        fun node -> nodeChildren node |> List.exists (fun c -> nodeType c = "interpolation")
      IsDefaultParameterValue =
        fun node ->
            // decision: compares node identity by `.id`, not reference equality — see the matching
            // comment in python.ts's isFormattedOrInterpolatedString for why.
            // decision: a default value isn't nested inside the `parameter` node itself —
            // function_value_parameters is a flat seq(parameter_modifiers?, parameter, ('=' expr)?), so
            // the default value's siblings (not ancestors) are the '=' token and the parameter.
            match nodeParent node with
            | Some parent when nodeType parent = "function_value_parameters" ->
                let siblings = nodeChildren parent

                match List.tryFindIndex (fun c -> nodeId c = nodeId node) siblings with
                | Some index when index >= 2 ->
                    match List.tryItem (index - 1) siblings, List.tryItem (index - 2) siblings with
                    | Some prev, Some prevPrev -> nodeType prev = "=" && nodeType prevPrev = "parameter"
                    | _ -> false
                | _ -> false
            | _ -> false
      // decision: true/false have no dedicated literal node in this grammar — they lex as plain
      // `identifier` tokens (verified: no boolean_literal rule exists). Safe to key off text since
      // true/false are hard keywords in Kotlin, not shadowable identifiers.
      IsBooleanLiteral =
        fun node ->
            if nodeType node <> "identifier" then
                false
            else
                let t = nodeText node

                t = "true" || t = "false"
      // decision: every call argument (named or positional) wraps in `value_argument`, so unlike the
      // other adapters' direct-parent check, this also has to rule out a named argument (`retries =
      // true`) by checking the literal is value_argument's *first* child — a named argument's
      // value_argument instead starts with `identifier '='` before the value.
      IsPositionalCallArgument =
        fun node ->
            match nodeParent node with
            | Some valueArgument when nodeType valueArgument = "value_argument" ->
                match nodeParent valueArgument with
                | Some vaParents when nodeType vaParents = "value_arguments" ->
                    match nodeParent vaParents with
                    | Some callParent when nodeType callParent = "call_expression" ->
                        match List.tryItem 0 (nodeChildren valueArgument) with
                        | Some first -> nodeId first = nodeId node
                        | None -> false
                    | _ -> false
                | _ -> false
            | _ -> false
      IsExplicitConstant =
        fun node ->
            match nodeType node with
            | "property_declaration" -> isConstPropertyDeclaration node
            | "assignment" -> isAnnotatedConstValMisparse node
            | _ -> false
      // `import a.b.C` -> source 'a.b' (the package, one symbol per line here since Kotlin has no
      // brace-grouped import syntax); `import a.b.*` -> source 'a.b' as-is, the qualified_identifier
      // is already the package with no trailing symbol to strip.
      ImportSource =
        fun node ->
            match nodeChildren node |> List.tryFind (fun c -> nodeType c = "qualified_identifier") with
            | Some qualified when nodeText qualified <> "" ->
                let text = nodeText qualified

                if nodeChildren node |> List.exists (fun c -> nodeType c = "*") then
                    text
                else
                    match text.LastIndexOf('.') with
                    | -1 -> text
                    | idx -> text.Substring(0, idx)
            | _ -> nodeText node
      ClassDefinitionNodeTypes = [ "class_declaration" ]
      GetClassName =
        fun node ->
            nodeChildren node
            |> List.tryFind (fun c -> nodeType c = "identifier")
            |> Option.map nodeText
      // `class Foo : Bar(), Baz` -> ['Bar', 'Baz']. Each delegation_specifier wraps either a
      // constructor_invocation (a superclass call, `Bar()`) or a bare user_type (an interface, `Baz`)
      // — both nest their name one level deeper inside a user_type node.
      GetBaseClassNames =
        fun node ->
            match
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = "delegation_specifiers")
            with
            | Some specifiers ->
                nodeChildren specifiers
                |> List.filter (fun s -> nodeType s = "delegation_specifier")
                |> List.choose delegationSpecifierName
            | None -> [] }
