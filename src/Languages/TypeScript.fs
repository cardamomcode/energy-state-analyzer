module Energy.Languages.TypeScript

open Fable.Core
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// The TypeScript LanguageAdapter (port of src/languages/typescript.ts).
//
// tree-sitter-typescript is structurally close to Python's grammar: real block/body nodes, a
// distinct else_clause, and and/or (&&/||) each get their own node type as the operator-token child
// of a binary_expression. Every hook operates on a raw `Node` (Fable dynamic `obj`) through the
// TreeSitter typed accessors — matching how the TS reads `.type`/`.text`/`.children`/`.parent`/`.id`
// on `any` nodes. The TS guards every read with optional chaining (`node?.x`); that is defensive
// against a null node, but our detectors only ever hand these hooks real nodes reached by traversing
// from the root (or via the null-safe nodeParent accessor), so we read members directly and treat
// `.children` as an always-present list (empty when there are none).

// decision: shared by extractTypedParameter/extractReturnType below — both check for TS's
// type-annotation grammar node (`: <type>`); a literal repeated across both would trip the
// magic-string detector's own duplicate-string check.
let private typeAnnotationNodeType = "type_annotation"

// decision: shared by getClassName/getBaseClassNames below — both check a node's type against TS's
// grammar node-type name for a class's own name identifier (distinct from a plain `identifier`,
// which lower-cased bindings use); a literal repeated across both would trip the magic-string
// detector's own duplicate-string check.
let private typeIdentifierNodeType = "type_identifier"

// decision: split out of getBaseClassNames into their own functions, each checking a single node
// type, rather than several `c.type === '...'` comparisons against the same `heritage` subtree in
// one function — that shape is exactly what the primitive-obsession detector's stringly-typed
// control-flow check flags as a switch-like branch on an ad hoc string tag.
let private extendsTargetNames (heritage: Node) : string list =
    let extendsClause =
        nodeChildren heritage |> List.tryFind (fun c -> nodeType c = "extends_clause")

    let extendsTarget =
        match extendsClause with
        | Some ec -> nodeChildren ec |> List.tryFind (fun c -> nodeType c <> "extends")
        | None -> None

    match extendsTarget with
    | Some t -> [ nodeText t ]
    | None -> []

let private implementsTargetNames (heritage: Node) : string list =
    let implementsClause =
        nodeChildren heritage
        |> List.tryFind (fun c -> nodeType c = "implements_clause")

    match implementsClause with
    | Some ic ->
        nodeChildren ic
        |> List.filter (fun c -> nodeType c = typeIdentifierNodeType)
        |> List.map nodeText
    | None -> []

// decision: treats arrow functions (`(x) => x + 1`) as `lambda`, matching Python's `lambda` — they
// add structural nesting in cognitive complexity but aren't analyzed by parameter-count/complexity/
// coherence themselves (same limitation Python already has for its own lambdas); only named
// `function_declaration`s and class `method_definition`s count as "a function" for those detectors.
//
// tradeoff: accepts a slightly higher cognitive-complexity score for `else if` chains (else_clause's
// flat +1 plus the nested if's `1 + nesting`) instead of unwrapping single-if else-clauses specially
// — TypeScript's `else if` parses as `else_clause` wrapping a nested `if_statement`, unlike Python's
// flat elif sibling.
let TYPESCRIPT: LanguageAdapter =
    { Id = "typescript"
      GrammarPath = "grammars/tree-sitter-typescript.wasm"
      NodeTypes =
        { Block = Some "statement_block"
          Parameters = "formal_parameters"
          IfStatement = Some "if_statement"
          ElseClause = Some "else_clause"
          ForStatement = Some "for_statement"
          WhileStatement = Some "while_statement"
          ConditionalExpression = Some "ternary_expression"
          Lambda = Some "arrow_function"
          ImportStatement = Some "import_statement"
          // import_statement already covers every import form.
          ImportFromStatement = None
          ExpressionStatement = Some "expression_statement"
          Assignment = Some "lexical_declaration"
          Module = Some "program"
          ExportStatement = Some "export_statement"
          Comment = Some "comment"
          // TS doesn't distinguish int/float, both are "number".
          IntegerLiteral = Some "number"
          FloatLiteral = None
          StringLiteral = Some "string" }
      IsFunctionDefinition = fun node -> nodeType node = "function_declaration" || nodeType node = "method_definition"
      ParameterChildTypes = [ "required_parameter"; "optional_parameter" ]
      DecisionNodeTypes =
        [ "if_statement"
          "for_statement"
          "for_in_statement"
          "while_statement"
          "catch_clause"
          "ternary_expression" ]
      CognitiveNestedDecisionTypes =
        [ "if_statement"
          "for_statement"
          "for_in_statement"
          "while_statement"
          "catch_clause" ]
      NestingControlTypes =
        [ "if_statement"
          "for_statement"
          "for_in_statement"
          "while_statement"
          "try_statement" ]
      GetBooleanOperator =
        fun node ->
            if nodeType node <> "binary_expression" then
                None
            else
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = "&&" || nodeType c = "||")
                |> Option.map (fun c -> if nodeType c = "&&" then And else Or)
      EntersNestedScope = fun node -> nodeType node = "statement_block"
      // JS/TS try/catch has no else-branch construct.
      IsTryElseClause = fun _ -> false
      VariableReferenceNodeTypes = [ "identifier"; "member_expression" ]
      ExtractTypedParameter =
        fun node ->
            if nodeType node <> "required_parameter" && nodeType node <> "optional_parameter" then
                None
            else
                let nameNode =
                    nodeChildren node |> List.tryFind (fun c -> nodeType c = "identifier")

                let typeAnnotation =
                    nodeChildren node |> List.tryFind (fun c -> nodeType c = typeAnnotationNodeType)

                match nameNode, typeAnnotation with
                | Some n, Some ta ->
                    // type_annotation's children are [':', <the actual type node>].
                    match nodeChildren ta |> List.tryFind (fun c -> nodeType c <> ":") with
                    | Some tn ->
                        Some
                            { Name = nodeText n
                              Type = nodeText tn }
                    | None -> None
                | _ -> None
      ExtractReturnType =
        fun node ->
            // decision: scans only the function node's own direct children — a parameter's
            // type_annotation lives two levels deeper (inside formal_parameters), so this can't
            // accidentally pick up a parameter's type instead of the return type.
            match nodeChildren node |> List.tryFind (fun c -> nodeType c = typeAnnotationNodeType) with
            | Some ta ->
                match nodeChildren ta |> List.tryFind (fun c -> nodeType c <> ":") with
                | Some tn -> Some(nodeText tn)
                | None -> None
            | None -> None
      GenericBrackets = { Open = "<"; Close = ">" }
      PrimitiveTypeNames = Set.ofList [ "string"; "number"; "boolean" ]
      // TS has no enforced-keyword-only parameter syntax — see language.ts's field doc.
      KeywordOnlyBoundaryTypes = []
      DistinctTypeAdvice = "a branded/nominal type (e.g. a tagged type alias)"
      GetEqualityComparisons =
        fun node ->
            if nodeType node <> "binary_expression" then
                []
            else
                match
                    nodeChildren node
                    |> List.tryFind (fun c -> nodeType c = "===" || nodeType c = "==")
                with
                | Some opToken ->
                    // decision: compare operand identity by `.id`, not structural equality.
                    let operands =
                        nodeChildren node |> List.filter (fun c -> nodeId c <> nodeId opToken)

                    match operands with
                    | [ l; r ] -> [ { Left = l; Right = r } ]
                    | _ -> []
                | None -> []
      // TS's set-membership idiom is `[...].includes(x)`, a call_expression rather than a comparison
      // node — not modeled here; repeated equality checks still accumulate via getEqualityComparisons.
      GetMembershipComparisons = fun _ -> []
      // `else if` has no flat elif node in this grammar — it's a nested if_statement one level inside
      // else_clause, which the match-opportunity detector walks itself.
      GetElseIfBranches = fun _ -> []
      SubscriptNodeTypes = [ "subscript_expression" ]
      // TS has no node type where an interpolated/formatted string still shares stringLiteral's node
      // type — template literals parse as `template_string`, which the traversal never visits as a
      // string literal in the first place.
      IsFormattedOrInterpolatedString = fun _ -> false
      IsDefaultParameterValue =
        fun node ->
            // decision: compares node identity by `.id`, not reference equality — see the matching
            // comment in python.ts's isFormattedOrInterpolatedString for why (web-tree-sitter mints a
            // fresh JS wrapper on every accessor, so `.id` is the stable identity).
            // decision: a parameter with a default value (`x: number = 1`) still parses as
            // `required_parameter`, not `optional_parameter` (that node type is reserved for `x?:
            // number`) — so this checks for the `=` child rather than trusting the parameter's own
            // node type to signal "has a default".
            match nodeParent node with
            | Some parent when nodeType parent = "required_parameter" || nodeType parent = "optional_parameter" ->
                let pc = nodeChildren parent

                List.exists (fun c -> nodeType c = "=") pc
                && (match List.tryItem (List.length pc - 1) pc with
                    | Some last -> nodeId last = nodeId node
                    | None -> false)
            | _ -> false
      IsBooleanLiteral = fun node -> nodeType node = "true" || nodeType node = "false"
      // An object-literal field (`{ retries: true }`) wraps the literal in `pair` inside `object`, so
      // a labeled boolean's parent is never `arguments` directly.
      IsPositionalCallArgument =
        fun node ->
            match nodeParent node with
            | Some parent ->
                match nodeParent parent with
                | Some gp -> nodeType parent = "arguments" && nodeType gp = "call_expression"
                | None -> false
            | None -> false
      // TS's `const` is block-scoping, not a compile-time-constant marker (unlike Kotlin's `const val`)
      // — module-scope lexical_declaration is the only signal here.
      IsExplicitConstant = fun _ -> false
      // The module specifier (the `string` child) is the dependency; everything in import_clause
      // (named/default/namespace imports) is just which symbols come from it.
      ImportSource =
        fun node ->
            match nodeChildren node |> List.tryFind (fun c -> nodeType c = "string") with
            | Some s -> nodeText s
            | None -> nodeText node
      // `abstract class Foo` parses as its own node type, distinct from a plain `class Foo`.
      ClassDefinitionNodeTypes = [ "class_declaration"; "abstract_class_declaration" ]
      GetClassName =
        fun node ->
            nodeChildren node
            |> List.tryFind (fun c -> nodeType c = typeIdentifierNodeType)
            |> Option.map nodeText
      // `class Foo extends Bar implements Baz, Qux {}` -> ['Bar', 'Baz', 'Qux']. extends_clause wraps
      // a single expression (usually an identifier, occasionally `Foo.Bar` as a member_expression);
      // implements_clause lists one or more type_identifier siblings directly.
      GetBaseClassNames =
        fun node ->
            match nodeChildren node |> List.tryFind (fun c -> nodeType c = "class_heritage") with
            | Some heritage -> extendsTargetNames heritage @ implementsTargetNames heritage
            | None -> [] }
