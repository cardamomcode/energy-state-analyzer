module Energy.Languages.Python

open Fable.Core
open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

// The Python LanguageAdapter (port of src/languages/python.ts).
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
let private typeAnnotationNodeType = "type"

// decision: shared by IsPositionalCallArgument and GetBaseClassNames below — both check a node's
// type against Python's grammar node-type name for a call's parenthesized argument list (a function
// call in the former, a class's base-class list in the latter, since Python's grammar reuses the
// same node shape for both).
let private argumentListNodeType = "argument_list"

// decision: shared by IsFormattedOrInterpolatedString and GetBaseClassNames below — both check a
// node's type against Python's grammar node-type name for a dotted attribute access (`a.b.C`).
let private attributeNodeType = "attribute"

let PYTHON : LanguageAdapter =
    { Id = "python"
      GrammarPath = "grammars/tree-sitter-python.wasm"
      NodeTypes =
          { Block = Some "block"
            Parameters = "parameters"
            IfStatement = Some "if_statement"
            ElseClause = Some "else_clause"
            ForStatement = Some "for_statement"
            WhileStatement = Some "while_statement"
            ConditionalExpression = Some "conditional_expression"
            Lambda = Some "lambda"
            ImportStatement = Some "import_statement"
            ImportFromStatement = Some "import_from_statement"
            ExpressionStatement = Some "expression_statement"
            Assignment = Some "assignment"
            Module = Some "module"
            ExportStatement = None
            Comment = Some "comment"
            IntegerLiteral = Some "integer"
            FloatLiteral = Some "float"
            StringLiteral = Some "string" }
      IsFunctionDefinition = fun node -> nodeType node = "function_definition"
      ParameterChildTypes = [ "identifier"; "default_parameter" ]
      DecisionNodeTypes =
          [ "if_statement"
            "elif_clause"
            "while_statement"
            "for_statement"
            "except_clause"
            "conditional_expression"
            "match_statement" ]
      CognitiveNestedDecisionTypes =
          [ "if_statement"
            "elif_clause"
            "for_statement"
            "while_statement"
            "except_clause"
            "match_statement" ]
      NestingControlTypes =
          [ "if_statement"
            "for_statement"
            "while_statement"
            "with_statement"
            "try_statement"
            "match_statement" ]
      GetBooleanOperator =
          fun node ->
              if nodeType node = "boolean_operator" then
                  nodeChildren node
                  |> List.tryFind (fun c -> nodeType c = "and" || nodeType c = "or")
                  |> Option.map (fun c -> if nodeType c = "and" then And else Or)
              else
                  None
      EntersNestedScope = fun node -> nodeType node = "block"
      // decision: `else_clause` is shared by if/for/while/try in tree-sitter-python, but only a
      // try's else is a real decision point (mirrors ruff's C901: a non-vacuous try-else adds 1).
      // if/for/while's else already scores 0 via DecisionNodeTypes — this predicate exists to avoid
      // conflating try's else with those.
      IsTryElseClause =
          fun node ->
              nodeType node = "else_clause"
              && (match nodeParent node with Some p -> nodeType p = "try_statement" | None -> false)
      VariableReferenceNodeTypes = [ "identifier"; "attribute" ]
      ExtractTypedParameter =
          fun node ->
              if nodeType node = "typed_parameter" || nodeType node = "typed_default_parameter" then
                  let nameNode = nodeChildren node |> List.tryFind (fun c -> nodeType c = "identifier")
                  let typeNode = nodeChildren node |> List.tryFind (fun c -> nodeType c = typeAnnotationNodeType)

                  match nameNode, typeNode with
                  | Some n, Some t -> Some { Name = nodeText n; Type = nodeText t }
                  | _ -> None
              else
                  None
      ExtractReturnType =
          fun node ->
              match nodeChildren node |> List.tryFindIndex (fun c -> nodeType c = "->") with
              | Some arrowIndex ->
                  let children = nodeChildren node

                  if arrowIndex + 1 < List.length children then
                      let typeNode = children.[arrowIndex + 1]

                      if nodeType typeNode = typeAnnotationNodeType then
                          Some (nodeText typeNode)
                      else
                          None
                  else
                      None
              | None -> None
      GenericBrackets = { Open = "["; Close = "]" }
      PrimitiveTypeNames = Set.ofList [ "str"; "int"; "float"; "bool"; "bytes" ]
      KeywordOnlyBoundaryTypes = [ "keyword_separator"; "list_splat_pattern" ]
      DistinctTypeAdvice = "NewType or a dataclass"
      GetEqualityComparisons =
          fun node ->
              if nodeType node = "comparison_operator" then
                  let children = nodeChildren node

                  children
                  |> List.mapi (fun i c -> i, c)
                  |> List.filter (fun (i, _) -> i >= 1 && i + 1 < List.length children)
                  |> List.filter (fun (_, c) -> nodeType c = "==")
                  |> List.map (fun (i, _) -> { Left = children.[i - 1]; Right = children.[i + 1] })
              else
                  []
      // decision: only Python gets this — TS's equivalent is a `.includes()` call expression (not a
      // comparison node) and F# has no direct construct; both still accumulate distinct literals across
      // separate equality comparisons via GetEqualityComparisons.
      GetMembershipComparisons =
          fun node ->
              if nodeType node = "comparison_operator" then
                  let children = nodeChildren node

                  children
                  |> List.mapi (fun i c -> i, c)
                  |> List.filter (fun (i, _) -> i >= 1 && i + 1 < List.length children)
                  |> List.filter (fun (_, c) -> nodeType c = "in")
                  |> List.map (fun (i, _) -> i - 1, i + 1)
                  |> List.choose (fun (leftIdx, rightIdx) ->
                          let left = children.[leftIdx]
                          let right = children.[rightIdx]

                          if nodeType right = "tuple" || nodeType right = "list" || nodeType right = "set" then
                              // decision: scan the named string children; stop at the first non-string named
                              // child (the fold's `failed` flag signals "not all strings", mirroring the TS
                              // `allStrings = false; break`. A tuple-state avoids an option accumulator, which
                              // Fable's transform can't lower inside a nested List.fold.
                              let unquote (s: string) = s.Substring(1, s.Length - 2)
                              let step (failed: bool) (acc: string list) (child: Node) =
                                  if failed then
                                      (true, acc)
                                  // decision: skips unnamed punctuation before checking literal shape — tuple
                                  // commas and brackets are structural tokens, not membership values.
                                  elif not (nodeIsNamed child) then
                                      (false, acc)
                                  elif nodeType child <> "string" then
                                      (true, acc)
                                  else
                                      (false, acc @ [ unquote (nodeText child) ])
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
      GetElseIfBranches =
          fun node ->
              nodeChildren node |> List.filter (fun c -> nodeType c = "elif_clause")
      SubscriptNodeTypes = [ "subscript" ]
      // decision: compares node identity by `.id`, not reference equality — web-tree-sitter mints a
      // fresh JS wrapper object on every `.children`/`.parent` access, so two accessors that reach the
      // same underlying tree node are not reference-equal even though `.id` matches.
      IsFormattedOrInterpolatedString =
          fun node ->
              match nodeChildren node |> List.tryFind (fun c -> nodeType c = "interpolation") with
              | Some _ -> true
              | None ->
                  match nodeParent node with
                  | Some parent ->
                      let parentChildren = nodeChildren parent
                      // decision: wrap the pipeline in parens so `= Some (...)` is parsed as an equality
                      // check, not a stray binding — F# reads the line break before `=` as ending the value.
                      let firstChildIdOk =
                          let firstChildId = parentChildren |> List.tryItem 0 |> Option.map nodeId
                          firstChildId = Some (nodeId node)

                      if nodeType parent = "binary_operator"
                         && firstChildIdOk
                         && parentChildren |> List.exists (fun c -> nodeType c = "%")
                      then
                          true
                      else if nodeType parent = attributeNodeType && firstChildIdOk then
                          match parentChildren |> List.tryFind (fun c -> nodeType c = "identifier") with
                          | Some m when nodeText m = "format" ->
                              (match nodeParent parent with Some pp -> nodeType pp = "call" | None -> false)
                          | _ -> false
                      else
                          false
                  | None -> false
      IsDefaultParameterValue =
          fun node ->
              match nodeParent node with
              | Some parent ->
                  let pc = nodeChildren parent

                  (nodeType parent = "default_parameter" || nodeType parent = "typed_default_parameter")
                  && List.length pc > 0
                  && nodeId (pc.[List.length pc - 1]) = nodeId node
              | None -> false
      IsBooleanLiteral = fun node -> nodeType node = "true" || nodeType node = "false"
      // A keyword argument (`retries=True`) wraps the literal in its own `keyword_argument` node, so a
      // labeled boolean's parent is never `argument_list` directly.
      IsPositionalCallArgument =
          fun node ->
              match nodeParent node with
              | Some parent ->
                  nodeType parent = argumentListNodeType
                  && (match nodeParent parent with Some pp -> nodeType pp = "call" | None -> false)
              | None -> false
      // Python has no dedicated compile-time-constant marker — module-scope assignment is the only
      // signal (see isInConstantContext in magicNumber.ts).
      IsExplicitConstant = fun _ -> false
      // `import os` -> 'os'; `from foo.bar import a, b, c` -> 'foo.bar' (the names after `import` are
      // irrelevant — they're all the same dependency). `import os, sys` (two unrelated modules on one
      // line) is rare enough that only the first is used as the source — undercounting a line like that
      // is the safe direction, since it only reduces false positives.
      ImportSource =
          fun node ->
              let children = nodeChildren node

              if nodeType node = "import_from_statement" then
                  match children |> List.tryFindIndex (fun c -> nodeType c = "import") with
                  | Some idx when idx >= 1 -> nodeText children.[idx - 1]
                  | _ -> nodeText node
              else
                  match children |> List.tryFind (fun c -> nodeType c = "dotted_name") with
                  | Some d -> nodeText d
                  | None -> nodeText node
      ClassDefinitionNodeTypes = [ "class_definition" ]
      GetClassName =
          fun node ->
              nodeChildren node
              |> List.tryFind (fun c -> nodeType c = "identifier")
              |> Option.map nodeText
      // `class Foo(Bar, Baz):` -> ['Bar', 'Baz']; `class Foo(meta=Meta):` skips the keyword_argument
      // (not a base class); `class Foo(pkg.Bar):` -> ['pkg.Bar'] via the attribute node's own text.
      GetBaseClassNames =
          fun node ->
              match nodeChildren node |> List.tryFind (fun c -> nodeType c = argumentListNodeType) with
              | Some argumentList ->
                  argumentList
                  |> nodeChildren
                  |> List.filter (fun c -> nodeType c = "identifier" || nodeType c = attributeNodeType)
                  |> List.map nodeText
              | None -> [] }
