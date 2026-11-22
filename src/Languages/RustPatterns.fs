// # RustPatterns
//
// Rust case-pattern placement tells the magic-string detector where a string literal is a refutable
// pattern: match arms, `if let`/`while let`, and `matches!` invocations. tree-sitter leaves macro
// arguments as flat token trees, so `matches!` is read token by token: the pattern is everything after
// the first top-level comma and before a top-level `if` guard. Other macros' token trees are never
// interpreted. Rust.fs exposes the result through the adapter's CasePatterns field.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Languages.RustPatterns

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter

/// Name the standard-library macro whose second argument is a pattern.
[<Literal>]
let private MatchesMacro = "matches"

/// Test whether a node has the supplied Rust grammar kind.
let private is kindName node = nodeType node = NodeType kindName

/// Recognize the argument token tree of `matches!`, either as a parsed invocation (`matches!(…)` or a path
/// ending in `matches`) or as the tokens `matches` `!` followed by a tree inside another macro's arguments,
/// as in `assert!(matches!(…))`.
let private isMatchesArguments (tree: Node) =
    match nodeParent tree with
    | Some invocation when is "macro_invocation" invocation ->
        nodeField "macro" invocation
        |> Option.bind (fun macro ->
            if is "scoped_identifier" macro then
                nodeField "name" macro
            else
                Some macro)
        |> Option.exists (fun name -> nodeText name = MatchesMacro)
    | Some enclosing when is "token_tree" enclosing ->
        let preceding =
            nodeChildren enclosing
            |> List.takeWhile (fun sibling -> nodeId sibling <> nodeId tree)
            |> List.rev

        match preceding with
        | bang :: name :: _ -> nodeText bang = "!" && is "identifier" name && nodeText name = MatchesMacro
        | _ -> false
    | _ -> false

/// Check that a top-level item of a `matches!` argument tree lies in its pattern: after the first comma and
/// before any guard `if`.
///
/// invariant: guard tokens never count, because `Some(v) if v == "x"` compares "x" inside an unparsed
/// token tree rather than matching it as a pattern.
let private isInMatchesPattern (arguments: Node) (item: Node) =
    let tokens = nodeChildren arguments
    let indexOf predicate = tokens |> List.tryFindIndex predicate

    let position = indexOf (fun token -> nodeId token = nodeId item)

    let comma = indexOf (fun token -> not (nodeIsNamed token) && nodeText token = ",")

    let guard = indexOf (fun token -> not (nodeIsNamed token) && nodeText token = "if")

    match position, comma with
    | Some at, Some separator -> at > separator && guard |> Option.forall (fun start -> at < start)
    | _ -> false

/// Climb nested token trees from a literal to the nearest `matches!` argument tree and check the literal's
/// top-level item there; trees of other macros are transparent only while nested inside one.
let private isMatchesPattern (tree: Node) (literal: Node) =
    let rec climb current item =
        if isMatchesArguments current then
            isInMatchesPattern current item
        else
            match nodeParent current with
            | Some enclosing when is "token_tree" enclosing -> climb enclosing current
            | _ -> false

    is "token_tree" tree && climb tree literal

/// Place string literals inside match-arm, `if let`/`while let`, and `matches!` patterns, including
/// or-patterns and constructor patterns such as `Some("a" | "b")`.
let casePatterns =
    {
        Wrappers =
            [
                NodeType "or_pattern"
                NodeType "tuple_struct_pattern"
                NodeType "tuple_pattern"
                NodeType "slice_pattern"
                NodeType "struct_pattern"
                NodeType "field_pattern"
                NodeType "reference_pattern"
                NodeType "captured_pattern"
            ]
        IsPatternSlot =
            fun parent child ->
                (is "match_pattern" parent
                 && nodeField "condition" parent
                    |> Option.forall (fun guard -> nodeId guard <> nodeId child))
                || fieldPatternSlot (NodeType "let_condition") "pattern" parent child
                || isMatchesPattern parent child
    }
