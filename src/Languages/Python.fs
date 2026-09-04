module Energy.Languages.Python

open Fable.Core
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// The Python LanguageAdapter.
//
// Every hook operates on a raw `Node` (Fable dynamic `obj`) through the TreeSitter typed
// accessors — matching how the TS reads `.type`/`.text`/`.children`/`.parent`/`.id` on `any`
// nodes. The TS guards every read with optional chaining (`node?.x`); that is defensive against a
// null node, but our detectors only ever hand these hooks real nodes reached by traversing from the
// root (or via the null-safe nodeParent accessor), so we read members directly. Deeper navigation
// still uses the null-safe accessors — nodeParent -> option, nodeChildren -> list.

// decision: shared by ExtractTypedParameter/ExtractReturnType below — both check a node's type
// against Python's grammar node-type name for a type annotation ('type', wrapping either a plain
// identifier or a generic_type); factoring it into one binding keeps the literal to a single
// occurrence (the magic-string detector's own duplicate-string check, when we dogfood on F#).
let private typeAnnotationNodeType = NodeType "type"

// decision: shared by IsPositionalCallArgument and GetBaseClassNames below — both check a node's
// type against Python's grammar node-type name for a call's parenthesized argument list (a function
// call in the former, a class's base-class list in the latter, since Python's grammar reuses the
// same node shape for both).
let private argumentListNodeType = NodeType "argument_list"

// decision: shared by IsFormattedOrInterpolatedString and GetBaseClassNames below — both check a
// node's type against Python's grammar node-type name for a dotted attribute access (`a.b.C`).
let private attributeNodeType = NodeType "attribute"
let private identifierNodeType = NodeType "identifier"
let private typedDefaultParameterNodeType = NodeType "typed_default_parameter"
let private comparisonOperatorNodeType = NodeType "comparison_operator"
let private callNodeType = NodeType "call"
let private andOperatorNodeType = NodeType "and"

let private isFirstChild (node: Node) (parent: Node) =
    parent
    |> nodeChildren
    |> List.tryHead
    |> Option.map nodeId
    |> (=) (Some(nodeId node))

let private isFormatCall (parent: Node) =
    parent
    |> nodeChildren
    |> List.tryFind (fun child -> nodeType child = identifierNodeType)
    |> Option.exists (fun methodName -> nodeText methodName = "format")
    && (nodeParent parent |> Option.exists (fun call -> nodeType call = callNodeType))

let private isFormattedStringParent (node: Node) (parent: Node) =
    let firstChild = isFirstChild node parent

    match nodeType parent with
    | NodeType "binary_operator" ->
        firstChild
        && (parent
            |> nodeChildren
            |> List.exists (fun child -> nodeType child = NodeType "%"))
    | nodeType when nodeType = attributeNodeType -> firstChild && isFormatCall parent
    | _ -> false

let pythonLanguageAdapter: LanguageAdapter =
    { Id = "python"
      GrammarPath = "grammars/tree-sitter-python.wasm"
      NodeTypes =
        { Block = Some(NodeType "block")
          Parameters = NodeType "parameters"
          IfStatement = Some(NodeType "if_statement")
          ElseClause = Some(NodeType "else_clause")
          ForStatement = Some(NodeType "for_statement")
          WhileStatement = Some(NodeType "while_statement")
          ConditionalExpression = Some(NodeType "conditional_expression")
          Lambda = Some(NodeType "lambda")
          ImportStatement = Some(NodeType "import_statement")
          ImportFromStatement = Some(NodeType "import_from_statement")
          ExpressionStatement = Some(NodeType "expression_statement")
          Assignment = Some(NodeType "assignment")
          Module = Some(NodeType "module")
          ExportStatement = None
          Comment = Some(NodeType "comment")
          IntegerLiteral = Some(NodeType "integer")
          FloatLiteral = Some(NodeType "float")
          StringLiteral = Some(NodeType "string") }
      IsFunctionDefinition = fun node -> nodeType node = NodeType "function_definition"
      IsStaticMethod =
        fun node ->
            nodeParent node
            |> Option.filter (fun parent -> nodeType parent = NodeType "decorated_definition")
            |> Option.exists (fun decorated ->
                decorated
                |> nodeChildren
                |> List.exists (fun child -> nodeType child = NodeType "decorator" && nodeText child = "@staticmethod"))
      ParameterChildTypes = [ identifierNodeType; NodeType "default_parameter" ]
      DecisionNodeTypes =
        [ NodeType "if_statement"
          NodeType "elif_clause"
          NodeType "while_statement"
          NodeType "for_statement"
          NodeType "except_clause"
          NodeType "conditional_expression"
          NodeType "match_statement" ]
      CyclomaticBranchCount =
        fun node ->
            if nodeType node <> NodeType "match_statement" then
                None
            else
                let cases =
                    nodeNamedChildren node
                    |> List.collect nodeNamedChildren
                    |> List.filter (fun child -> nodeType child = NodeType "case_clause")

                let hasFallback =
                    cases
                    |> List.exists (fun caseClause -> nodeText caseClause |> _.Contains("case _"))

                Some(cases.Length + if hasFallback then 0 else 1)
      CognitiveNestedDecisionTypes =
        [ NodeType "if_statement"
          NodeType "elif_clause"
          NodeType "for_statement"
          NodeType "while_statement"
          NodeType "except_clause"
          NodeType "match_statement" ]
      NestingControlTypes =
        [ NodeType "if_statement"
          NodeType "for_statement"
          NodeType "while_statement"
          NodeType "with_statement"
          NodeType "try_statement"
          NodeType "match_statement" ]
      GetBooleanOperator =
        fun node ->
            if nodeType node = NodeType "boolean_operator" then
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = andOperatorNodeType || nodeType c = NodeType "or")
                |> Option.map (fun c -> if nodeType c = andOperatorNodeType then And else Or)
            else
                None
      EntersNestedScope = fun node -> nodeType node = NodeType "block"
      // decision: `else_clause` is shared by if/for/while/try in tree-sitter-python, but only a
      // try's else is a real decision point (mirrors ruff's C901: a non-vacuous try-else adds 1).
      // if/for/while's else already scores 0 via DecisionNodeTypes — this predicate exists to avoid
      // conflating try's else with those.
      IsTryElseClause =
        fun node ->
            nodeType node = NodeType "else_clause"
            && (match nodeParent node with
                | Some p -> nodeType p = NodeType "try_statement"
                | None -> false)
      VariableReferenceNodeTypes = [ identifierNodeType; attributeNodeType ]
      ExtractTypedParameter =
        fun node ->
            if
                nodeType node = NodeType "typed_parameter"
                || nodeType node = typedDefaultParameterNodeType
            then
                let nameNode =
                    nodeChildren node |> List.tryFind (fun c -> nodeType c = identifierNodeType)

                let typeNode =
                    nodeChildren node |> List.tryFind (fun c -> nodeType c = typeAnnotationNodeType)

                Option.map2 (fun n t -> { Name = nodeText n; Type = nodeText t }) nameNode typeNode
            else
                None
      ExtractReturnType =
        fun node ->
            match nodeChildren node |> List.tryFindIndex (fun c -> nodeType c = NodeType "->") with
            | Some arrowIndex ->
                let children = nodeChildren node

                if arrowIndex + 1 < List.length children then
                    let typeNode = children.[arrowIndex + 1]

                    if nodeType typeNode = typeAnnotationNodeType then
                        Some(nodeText typeNode)
                    else
                        None
                else
                    None
            | None -> None
      GenericBrackets = { Open = "["; Close = "]" }
      PrimitiveTypeNames = Set.ofList [ "str"; "int"; "float"; "bool"; "bytes" ]
      KeywordOnlyBoundaryTypes = [ NodeType "keyword_separator"; NodeType "list_splat_pattern" ]
      DistinctTypeAdvice = "NewType or a dataclass"
      GetEqualityComparisons =
        fun node ->
            if nodeType node = comparisonOperatorNodeType then
                let children = nodeChildren node

                children
                |> List.mapi (fun i c -> i, c)
                |> List.filter (fun (i, _) -> i >= 1 && i + 1 < List.length children)
                |> List.filter (fun (_, c) -> nodeType c = NodeType "==")
                |> List.map (fun (i, _) ->
                    { Left = children.[i - 1]
                      Right = children.[i + 1] })
            else
                []
      // decision: only Python gets this — TS's equivalent is a `.includes()` call expression (not a
      // comparison node) and F# has no direct construct; both still accumulate distinct literals across
      // separate equality comparisons via GetEqualityComparisons.
      GetMembershipComparisons =
        fun node ->
            if nodeType node = comparisonOperatorNodeType then
                let children = nodeChildren node

                children
                |> List.mapi (fun i c -> i, c)
                |> List.filter (fun (i, _) -> i >= 1 && i + 1 < List.length children)
                |> List.filter (fun (_, c) -> nodeType c = NodeType "in")
                |> List.map (fun (i, _) -> i - 1, i + 1)
                |> List.choose (fun (leftIdx, rightIdx) ->
                    let left = children.[leftIdx]
                    let right = children.[rightIdx]

                    if
                        nodeType right = NodeType "tuple"
                        || nodeType right = NodeType "list"
                        || nodeType right = NodeType "set"
                    then
                        // decision: scan the named string children; stop at the first non-string named
                        // child (the fold's `failed` flag signals "not all strings", mirroring the TS
                        // `allStrings = false; break`. A tuple-state avoids an option accumulator, which
                        // Fable's transform can't lower inside a nested List.fold.
                        let unquote (s: string) = s.Substring(1, s.Length - 2)

                        let step (failed: bool) (acc: string list) (child: Node) =
                            if failed then (true, acc)
                            // decision: skips unnamed punctuation before checking literal shape — tuple
                            // commas and brackets are structural tokens, not membership values.
                            elif not (nodeIsNamed child) then (false, acc)
                            elif nodeType child <> NodeType "string" then (true, acc)
                            else (false, acc @ [ unquote (nodeText child) ])

                        let failed, values =
                            nodeChildren right
                            |> List.fold (fun (failed, acc) child -> step failed acc child) (false, [])

                        match failed with
                        | true -> None
                        | false ->
                            if values.Length > 0 then
                                Some { Left = left; Values = values }
                            else
                                None
                    else
                        None)
            else
                []
      IsMatchCaseLiteral =
        fun node ->
            nodeType node = NodeType "string"
            || nodeType node = NodeType "integer"
            || nodeType node = NodeType "float"
      GetElseIfBranches = fun node -> nodeChildren node |> List.filter (fun c -> nodeType c = NodeType "elif_clause")
      SubscriptNodeTypes = [ NodeType "subscript" ]
      // decision: compares node identity by `.id`, not reference equality — web-tree-sitter mints a
      // fresh JS wrapper object on every `.children`/`.parent` access, so two accessors that reach the
      // same underlying tree node are not reference-equal even though `.id` matches.
      IsFormattedOrInterpolatedString =
        fun node ->
            match
                nodeChildren node
                |> List.tryFind (fun c -> nodeType c = NodeType "interpolation")
            with
            | Some _ -> true
            | None -> nodeParent node |> Option.exists (isFormattedStringParent node)
      IsDefaultParameterValue =
        fun node ->
            match nodeParent node with
            | Some parent ->
                let pc = nodeChildren parent

                (nodeType parent = NodeType "default_parameter"
                 || nodeType parent = typedDefaultParameterNodeType)
                && List.length pc > 0
                && nodeId (pc.[List.length pc - 1]) = nodeId node
            | None -> false
      IsBooleanLiteral = fun node -> nodeType node = NodeType "true" || nodeType node = NodeType "false"
      // A keyword argument (`retries=True`) wraps the literal in its own `keyword_argument` node, so a
      // labeled boolean's parent is never `argument_list` directly.
      IsPositionalCallArgument =
        fun node ->
            match nodeParent node with
            | Some parent ->
                nodeType parent = argumentListNodeType
                && (match nodeParent parent with
                    | Some pp -> nodeType pp = callNodeType
                    | None -> false)
            | None -> false
      // Python has no dedicated compile-time-constant marker — module-scope assignment is the only
      // signal (see isInConstantContext in magicNumber.ts).
      IsExplicitConstant = fun _ -> false
      // Preserve `from` bindings separately from module imports: the former expands the local vocabulary,
      // while the latter retains qualified use. A multi-source `import a, b` yields both dependencies.
      ImportInfo =
        fun node ->
            if nodeType node = NodeType "import_from_statement" then
                let text = nodeText node
                let importIndex = text.IndexOf(" import ", System.StringComparison.Ordinal)

                if importIndex > 5 then
                    let source = text.Substring(5, importIndex - 5).Trim()
                    let names = text.Substring(importIndex + 8).Trim().Trim([| '('; ')' |])

                    if names = "*" then
                        [ { Kind = Wildcard
                            Source = source
                            Bindings = [] } ]
                    else
                        let bindings =
                            names.Split(',')
                            |> Array.toList
                            |> List.map (fun name -> name.Trim())
                            |> List.filter (fun name -> name <> "")
                            |> List.map (fun name ->
                                let parts = name.Split([| ' ' |], System.StringSplitOptions.RemoveEmptyEntries)
                                let imported = parts.[0]

                                let local =
                                    if parts.Length >= 3 && parts.[1] = "as" then
                                        parts.[2]
                                    else
                                        imported

                                { ImportedName = imported
                                  LocalName = local })

                        [ { Kind = Members
                            Source = source
                            Bindings = bindings } ]
                else
                    [ { Kind = Members
                        Source = text
                        Bindings = [] } ]
            else
                nodeText node
                |> fun text -> text.Substring(7).Split(',')
                |> Array.toList
                |> List.map (fun item ->
                    let parts =
                        item.Trim().Split([| ' ' |], System.StringSplitOptions.RemoveEmptyEntries)

                    let source = parts.[0]

                    { Kind = Module
                      Source = source
                      Bindings = [] })
      IsClassDefinition = fun node -> nodeType node = NodeType "class_definition"
      GetClassName =
        fun node ->
            nodeChildren node
            |> List.tryFind (fun c -> nodeType c = identifierNodeType)
            |> Option.map nodeText
      // `class Foo(Bar, Baz):` -> ['Bar', 'Baz']; `class Foo(meta=Meta):` skips the keyword_argument
      // (not a base class); `class Foo(pkg.Bar):` -> ['pkg.Bar'] via the attribute node's own text.
      GetBaseClassNames =
        fun node ->
            match nodeChildren node |> List.tryFind (fun c -> nodeType c = argumentListNodeType) with
            | Some argumentList ->
                argumentList
                |> nodeChildren
                |> List.filter (fun c -> nodeType c = identifierNodeType || nodeType c = attributeNodeType)
                |> List.map nodeText
            | None -> []
      ErrorHandlingAnchorTypes = [ NodeType "try_statement" ] }
