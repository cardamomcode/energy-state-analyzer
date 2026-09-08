module Energy.Languages.FSharp

open Fable.Core
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// The F# LanguageAdapter.
//
// tree-sitter-fsharp has no block/body wrapper node — an if/for/while's branches are direct
// expression children of the construct itself (there's no equivalent of Python's `block` or
// TypeScript's `statement_block`), and `else` isn't a distinct node type either (an else-if chain is
// just a nested if_expression in the `else` position). Boolean operators (&&/||) aren't their own
// node type: they're an `infix_expression` whose `infix_op` child's text happens to be "&&" or "||",
// same shape as `+`/`-`/etc. Every hook operates on a raw `Node` (Fable dynamic `obj`) through the
// TreeSitter typed accessors; `.children` is an always-present list, read directly like Python's
// port (detectors only ever hand these hooks real nodes reached from the root).

// decision: shared by isFunctionDefinition/extractReturnType below — both check a node's type against
// this grammar node-type name; a literal repeated across both would trip the magic-string detector's
// own duplicate-string check.
let private functionDeclarationLeft = NodeType "function_declaration_left"

// decision: checks for a function_declaration_left child in isFunctionDefinition rather than matching
// function_or_value_defn alone — that node type also covers plain `let`/`let!` bindings (via
// value_declaration_left), and without the check every nested `let`/`let!` inside a function body
// (e.g. inside a MailboxProcessor `actor { let! msg = ... }`) would be misidentified as its own
// nested closure.
// invariant: a nested let/let! binding never inflates the enclosing function's cognitive complexity
// or nesting depth — only bindings with a function_declaration_left child count as a function.
// assumption: any literal that is the direct value of a `let` binding (top-level or nested) is
// already named and not flagged as a magic value — broader than Python's module-only rule, since
// function_or_value_defn -> declaration_expression looks identical at every scope, but still aligned
// with the detector's intent that `let NAME = ...` IS F#'s idiomatic way to name a constant.

// decision: an F# `and`-binding (`let rec f ... and g ...`, a mutually recursive let) parses as ONE
// function_or_value_defn holding several function_declaration_left heads, each with its own parameters
// and body — verified against the tree-sitter-fsharp parse tree. The shared detectors analyze "one
// function per definition node", so this splits the merged defn into one FunctionHead view per head:
// ParametersRoot is the head whose children carry its argument_patterns, Body is the expression
// immediately after that head's `=` (its own subtree, not the merged defn). A single-head defn — every
// other F# function and every other language — yields one view wrapping the defn itself, preserving the
// historical single-function behavior. Without this split, the second head's parameters are never
// analyzed (false negative) and the heads' string-literal comparisons accumulate under one variable
// name (false positive).
let private functionHeads (defn: Node) : FunctionHead list =
    let children = nodeChildren defn
    let heads = children |> List.filter (fun c -> nodeType c = functionDeclarationLeft)

    if heads.Length <= 1 then
        [ { ParametersRoot = defn; Body = defn } ]
    else
        heads
        |> List.map (fun head ->
            let headIndex = children |> List.findIndex (fun c -> nodeId c = nodeId head)
            // The head's `=` is the first `=` after it (an optional `:` <type> annotation may sit
            // between the head and its `=`, but carries no `=` of its own). The body is the node
            // immediately after that `=`.
            let afterHead = children |> List.skip (headIndex + 1)

            let body =
                match afterHead |> List.tryFindIndex (fun c -> nodeType c = NodeType "=") with
                | Some eqOffset when eqOffset + 1 < afterHead.Length -> afterHead |> List.item (eqOffset + 1)
                | _ -> defn

            { ParametersRoot = head; Body = body })

// decision: the idiomatic F# stringly-typed dispatch is a `match` on string literals — the form the
// if/elif-based stringly-typed check already catches is the non-idiomatic one. This extracts the
// scrutinee (only when it is a simple variable, so the dispatch can be attributed to one name) and the
// string-literal case patterns across the match's rules. A rule's pattern is its first named child;
// a string case is one whose pattern is (or wraps, e.g. in a const node) a `string` literal.
let private matchStringCases (node: Node) : (Node * Node list) option =
    if nodeType node <> NodeType "match_expression" then
        None
    else
        let named = nodeNamedChildren node

        let scrutinee =
            named |> List.tryFind (fun c -> nodeType c = NodeType "long_identifier_or_op")

        let stringCaseOf (rule: Node) : Node option =
            match nodeNamedChildren rule |> List.tryHead with
            | Some pattern ->
                if nodeType pattern = NodeType "string" then
                    Some pattern
                else
                    nodeNamedChildren pattern
                    |> List.tryFind (fun c -> nodeType c = NodeType "string")
            | None -> None

        let cases =
            named
            |> List.collect (fun c ->
                if nodeType c = NodeType "rules" then
                    c |> nodeNamedChildren |> List.choose stringCaseOf
                else
                    [])

        match scrutinee, cases with
        | Some s, cs when cs.Length > 0 -> Some(s, cs)
        | _ -> None

// decision: tree-sitter-fsharp parses a named-argument call `setField (name = "alpha")` as an
// application_expression whose parenthesized argument is an infix_expression with infix_op "=" — the
// identical shape of a genuine `a = b` comparison, which is why the equality hook (below) otherwise
// counts it as `name = "alpha"`. A node is in named-argument position when its ancestor chain reaches
// an application_expression while passing only through paren_expression/tuple_expression argument
// wrappers, and it sits on the argument side (not the callee). Record literals are unaffected: `{ Name
// = "one" }` parses as field_initializer, not infix_expression, so it never reaches this hook.
let private isNamedArgumentPosition (node: Node) : bool =
    let rec walk (n: Node) : bool =
        match nodeParent n with
        | Some p when
            nodeType p = NodeType "paren_expression"
            || nodeType p = NodeType "tuple_expression"
            ->
            walk p
        | Some p when nodeType p = NodeType "application_expression" ->
            // Confirm n is an argument, not the callee (the application's first child).
            match List.tryItem 0 (nodeChildren p) with
            | Some first -> nodeId first <> nodeId n
            | None -> true
        | _ -> false

    walk node

let private tryExpressionNodeType = NodeType "try_expression"
let private declarationExpressionNodeType = NodeType "declaration_expression"
let private rulesNodeType = NodeType "rules"
let private ruleNodeType = NodeType "rule"

// F# sequences `let` bindings through declaration_expression instead of a block. Expand only that
// structural chain; applications, conditionals, and loops remain one logical item.
let rec private logicalItems (node: Node) : Node list =
    match nodeType node with
    | kind when kind = declarationExpressionNodeType -> nodeNamedChildren node |> List.collect logicalItems
    | kind when kind = rulesNodeType -> nodeNamedChildren node |> List.collect logicalItems
    | kind when kind = ruleNodeType -> [ node ]
    | kind when kind = NodeType "function_or_value_defn" ->
        nodeNamedChildren node
        |> List.filter (fun child ->
            nodeType child <> functionDeclarationLeft
            && nodeType child <> NodeType "value_declaration_left")
        |> List.collect logicalItems
    | _ -> [ node ]

let private errorHandlingRegion (node: Node) : ErrorHandlingRegion option =
    if nodeType node <> tryExpressionNodeType then
        None
    else
        let children = nodeNamedChildren node

        let rulesIndex =
            children |> List.tryFindIndex (fun child -> nodeType child = rulesNodeType)

        match rulesIndex with
        | Some index ->
            { Anchor = node
              ProtectedItems = children |> List.take index |> List.collect logicalItems
              RecoveryItems = children |> List.skip index |> List.collect logicalItems }
            |> Some
        | None ->
            match children with
            | protectedBody :: cleanupBody :: _ ->
                { Anchor = node
                  ProtectedItems = logicalItems protectedBody
                  RecoveryItems = logicalItems cleanupBody }
                |> Some
            | _ -> None

let fSharpLanguageAdapter: LanguageAdapter =
    { Id = "fsharp"
      GrammarPath = "grammars/tree-sitter-fsharp.wasm"
      NodeTypes =
        { Block = None
          Parameters = NodeType "argument_patterns"
          IfStatement = Some(NodeType "if_expression")
          ElseClause = None
          ForStatement = Some(NodeType "for_expression")
          WhileStatement = Some(NodeType "while_expression")
          // ternary-position `if` reuses if_expression, already covered.
          ConditionalExpression = None
          Lambda = Some(NodeType "fun_expression")
          // `open X`.
          ImportStatement = Some(NodeType "import_decl")
          ImportFromStatement = None
          // F# has no string-literal docstring convention.
          ExpressionStatement = None
          Assignment = Some(NodeType "function_or_value_defn")
          Module = Some(NodeType "declaration_expression")
          ExportStatement = None
          Comment = Some(NodeType "line_comment")
          IntegerLiteral = Some(NodeType "int")
          FloatLiteral = Some(NodeType "float")
          StringLiteral = Some(NodeType "string") }
      IsFunctionDefinition =
        fun node ->
            nodeType node = NodeType "function_or_value_defn"
            && (nodeChildren node |> List.exists (fun c -> nodeType c = functionDeclarationLeft))
      GetFunctionHeads = functionHeads
      IsStaticMethod = fun _ -> false
      ParameterChildTypes = [ NodeType "long_identifier"; NodeType "typed_pattern" ]
      DecisionNodeTypes =
        [ NodeType "if_expression"
          NodeType "elif_expression"
          NodeType "for_expression"
          NodeType "while_expression"
          NodeType "try_expression"
          NodeType "match_expression" ]
      CyclomaticBranchCount =
        fun node ->
            if nodeType node <> NodeType "match_expression" then
                None
            else
                let rules =
                    nodeNamedChildren node
                    |> List.collect nodeNamedChildren
                    |> List.filter (fun child -> nodeType child = NodeType "rule")

                let hasFallback =
                    rules |> List.exists (fun rule -> nodeText rule |> _.Contains("_ ->"))

                Some(rules.Length + if hasFallback then 0 else 1)
      CognitiveNestedDecisionTypes =
        [ NodeType "if_expression"
          NodeType "elif_expression"
          NodeType "for_expression"
          NodeType "while_expression"
          NodeType "try_expression"
          NodeType "match_expression" ]
      NestingControlTypes =
        [ NodeType "if_expression"
          NodeType "elif_expression"
          NodeType "for_expression"
          NodeType "while_expression"
          NodeType "try_expression"
          NodeType "match_expression" ]
      GetBooleanOperator =
        fun node ->
            if nodeType node <> NodeType "infix_expression" then
                None
            else
                match nodeChildren node |> List.tryFind (fun c -> nodeType c = NodeType "infix_op") with
                | Some op ->
                    let t = nodeText op

                    if t = "&&" then Some And
                    elif t = "||" then Some Or
                    else None
                | None -> None
      // No block wrapper exists, so every child of a decision point is nested content.
      EntersNestedScope = fun _ -> true
      // F#'s try_expression has no else-branch construct.
      IsTryElseClause = fun _ -> false
      // long_identifier_or_op's own .text is already the bare (possibly dotted) name, so it's used as
      // is rather than unwrapped down to a leaf identifier.
      VariableReferenceNodeTypes = [ NodeType "long_identifier_or_op" ]
      ExtractTypedParameter =
        fun node ->
            if nodeType node <> NodeType "typed_pattern" then
                None
            else
                let children = nodeChildren node

                let patternNode =
                    children |> List.tryFind (fun c -> nodeType c = NodeType "identifier_pattern")

                // decision: the type is whatever node follows the `:` annotation — simple_type
                // (`x: int`), generic_type (`xs: Map<string, int>`), postfix_type (`b: string option`,
                // `xs: int list`), or any other annotated shape. Taking the node after `:` (rather than
                // matching a fixed set of type-node types) means a postfix/wrapper shape is no longer
                // silently dropped: its full text (e.g. `"string option"`) is never in
                // PrimitiveTypeNames, so it correctly acts as a distinct-type barrier between two
                // same-typed neighbors, and it stays visible to the type-cohesion signal.
                let typeNode =
                    children
                    |> List.tryFindIndex (fun c -> nodeType c = NodeType ":")
                    |> Option.bind (fun i ->
                        if i + 1 < children.Length then
                            Some(children |> List.item (i + 1))
                        else
                            None)

                match patternNode, typeNode with
                | Some p, Some t -> Some { Name = nodeText p; Type = nodeText t }
                | _ -> None
      ExtractReturnType =
        fun node ->
            if nodeType node <> NodeType "function_or_value_defn" then
                None
            else
                let declIndex =
                    nodeChildren node
                    |> List.tryFindIndex (fun c -> nodeType c = functionDeclarationLeft)

                let equalsIndex =
                    nodeChildren node |> List.tryFindIndex (fun c -> nodeType c = NodeType "=")

                match declIndex, equalsIndex with
                | Some di, Some ei ->
                    // decision: only trusts a `:` <type> pair sitting as a direct child strictly between
                    // the declaration head and `=` — tree-sitter-fsharp has been observed to fold a
                    // curried function's return-type annotation into its last parameter's typed_pattern
                    // instead of producing this clean shape when the function is parsed as a file's only
                    // statement. Returning null rather than reaching into a parameter node avoids
                    // misattributing a parameter's type as the return type.
                    let colonIndex =
                        nodeChildren node
                        |> List.tryFindIndex (fun c -> nodeType c = NodeType ":")
                        |> Option.bind (fun i -> if i > di && i < ei then Some i else None)

                    match colonIndex with
                    | Some ci when ci + 1 < List.length (nodeChildren node) ->
                        let children = nodeChildren node

                        Some(nodeText (List.item (ci + 1) children))
                    | _ -> None
                | _ -> None
      GenericBrackets = { Open = "<"; Close = ">" }
      PrimitiveTypeNames = Set.ofList [ "string"; "int"; "float"; "bool" ]
      // F#'s named-argument syntax is optional at the call site, so it doesn't prevent a future
      // positional call — not a valid swap-risk mitigation. See language.ts's field doc.
      KeywordOnlyBoundaryTypes = []
      DistinctTypeAdvice = "a single-case union type"
      GetEqualityComparisons =
        fun node ->
            // decision: combine the two early-exit guards (not-an-infix, and a named-argument position)
            // in one if rather than nesting them — a named-argument call (`setField (name = "alpha")`)
            // parses as the same infix `=` shape as a genuine comparison but is not one, so it must be
            // excluded before it can be miscounted as a stringly-typed literal comparison (see
            // isNamedArgumentPosition above).
            if nodeType node <> NodeType "infix_expression" || isNamedArgumentPosition node then
                []
            else
                match
                    nodeChildren node
                    |> List.tryFind (fun c -> nodeType c = NodeType "infix_op")
                    |> Option.filter (fun op -> nodeText op = "=")
                with
                | Some opToken ->
                    let operands =
                        nodeChildren node |> List.filter (fun c -> nodeId c <> nodeId opToken)

                    match operands with
                    | [ l; rawRight ] ->
                        // Literals are wrapped in a `const` node; unwrap so callers can compare .type
                        // against nodeTypes.stringLiteral directly, same as Python/TS.
                        let right =
                            if nodeType rawRight = NodeType "const" && (nodeChildren rawRight).Length = 1 then
                                List.head (nodeChildren rawRight)
                            else
                                rawRight

                        [ { Left = l; Right = right } ]
                    | _ -> []
                | None -> []
      GetMatchStringCases = matchStringCases
      // F# has no `x in (a, b, c)`-style membership construct; repeated equality checks (e.g. an elif
      // chain) still accumulate via getEqualityComparisons.
      GetMembershipComparisons = fun _ -> []
      IsMatchCaseLiteral =
        fun node ->
            nodeType node = NodeType "string"
            || nodeType node = NodeType "int"
            || nodeType node = NodeType "float"
      GetElseIfBranches =
        fun node ->
            nodeChildren node
            |> List.filter (fun c -> nodeType c = NodeType "elif_expression")
      // F# indexes via `.[i]` rather than a dedicated subscript node — left unmodeled rather than
      // guessed at with a fragile node-type match.
      SubscriptNodeTypes = []
      // F# also has interpolated ($"...") strings, but they aren't distinguished from plain strings
      // here — a known gap, same tradeoff as leaving callNodeTypes unmodeled above.
      IsFormattedOrInterpolatedString = fun _ -> false
      IsDefaultParameterValue = fun _ -> false
      // A bool literal parses as a `const` node wrapping a `bool` child (its .text is already
      // "true"/"false", same as Python/TS's dedicated literal node types).
      IsBooleanLiteral =
        fun node ->
            if nodeType node <> NodeType "const" then
                false
            else
                match nodeChildren node with
                | [ single ] -> nodeType single = NodeType "bool"
                | _ -> false
      // F# has no dedicated call-expression or argument-list node — curried application (`f true`) and
      // paren-tuple application (`f(true, x)`) both parse as application_expression, and named-argument
      // syntax (`retries = true`) reuses the same infix_expression node the primitive-obsession
      // detector treats as equality elsewhere in this grammar. Three shapes count as "positional":
      //   1. curried: `application_expression(callee, true)` — the literal is the argument (not callee)
      //      child of an application_expression directly.
      //   2. paren, single arg: `f(true)` — same shape as (1), the parens contribute no extra node.
      //   3. paren, multiple args: `f(true, x)` — the literal is a direct element of a tuple_expression
      //      that is itself an application_expression's argument.
      // A named argument's literal sits one level deeper, inside infix_expression (case 1/2's
      // application_expression child, or case 3's tuple_expression element) — never a direct child of
      // either, so it never matches below without a separate "is labeled" check.
      IsPositionalCallArgument =
        fun node ->
            match nodeParent node with
            | Some parent when nodeType parent = NodeType "application_expression" ->
                // decision: guard against the literal being the callee itself — never true in practice
                // (a bool can't be applied to arguments), kept for safety. Compares by `.id`.
                match List.tryItem 0 (nodeChildren parent) with
                | Some first -> nodeId first <> nodeId node
                | None -> true
            | Some parent when nodeType parent = NodeType "tuple_expression" ->
                match nodeParent parent with
                | Some gp -> nodeType gp = NodeType "application_expression"
                | None -> false
            | _ -> false
      // F# has no compile-time-constant marker distinct from an ordinary `let` binding — module-scope
      // binding is the only signal here.
      IsExplicitConstant = fun _ -> false
      // `open X.Y` makes all public names from the module available unqualified, so it is a scope
      // operation rather than a member import.
      ImportInfo =
        fun node ->
            let source =
                match
                    nodeChildren node
                    |> List.tryFind (fun c -> nodeType c = NodeType "long_identifier")
                with
                | Some li -> nodeText li
                | None -> nodeText node

            [ { Kind = ScopeOpen
                Source = source
                Bindings = [] } ]
      // F# has no idiomatic class-per-file OOP pattern the class-relatedness check targets — its
      // type_definition node also covers records/unions/modules, and distinguishing "this is a class
      // with methods" from those would need the same kind of grammar-shape disambiguation
      // isFunctionDefinition already does for function_or_value_defn, for a construct this codebase's
      // F# usage rarely reaches for. Left unmodeled; every method-bearing F# file is still covered by
      // the free-function checks above, unaffected by this gap.
      IsClassDefinition = fun _ -> false
      GetClassName = fun _ -> None
      GetBaseClassNames = fun _ -> []
      GetErrorHandlingRegion = errorHandlingRegion
      GetFunctionLogicalItems = logicalItems }
