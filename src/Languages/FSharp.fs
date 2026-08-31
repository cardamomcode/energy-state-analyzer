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
let FSHARP: LanguageAdapter =
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
                let patternNode =
                    nodeChildren node
                    |> List.tryFind (fun c -> nodeType c = NodeType "identifier_pattern")
                // decision: also matches generic_type (e.g. `xs: Iterable<'a>`), not just simple_type
                // (`x: int`) — a curried, generically-typed parameter is exactly the shape the
                // type-cohesion signal needs to see to recognize an F#-style single-type module.
                let typeNode =
                    nodeChildren node
                    |> List.tryFind (fun c ->
                        nodeType c = NodeType "simple_type" || nodeType c = NodeType "generic_type")

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
            if nodeType node <> NodeType "infix_expression" then
                []
            else
                match nodeChildren node |> List.tryFind (fun c -> nodeType c = NodeType "infix_op") with
                | Some opToken when nodeText opToken = "=" ->
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
                | _ -> []
      // F# has no `x in (a, b, c)`-style membership construct; repeated equality checks (e.g. an elif
      // chain) still accumulate via getEqualityComparisons.
      GetMembershipComparisons = fun _ -> []
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
      // `open X.Y` already names one whole module per line (F# has no per-symbol import), so the
      // long_identifier child is used as-is with no stripping.
      ImportSource =
        fun node ->
            match
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = NodeType "long_identifier")
            with
            | Some li -> nodeText li
            | None -> nodeText node
      // F# has no idiomatic class-per-file OOP pattern the class-relatedness check targets — its
      // type_definition node also covers records/unions/modules, and distinguishing "this is a class
      // with methods" from those would need the same kind of grammar-shape disambiguation
      // isFunctionDefinition already does for function_or_value_defn, for a construct this codebase's
      // F# usage rarely reaches for. Left unmodeled; every method-bearing F# file is still covered by
      // the free-function checks above, unaffected by this gap.
      ClassDefinitionNodeTypes = []
      GetClassName = fun _ -> None
      GetBaseClassNames = fun _ -> [] }
