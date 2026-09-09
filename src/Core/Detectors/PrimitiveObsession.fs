module Energy.Core.Detectors.PrimitiveObsession

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Detectors.ParameterCount

/// Named constants fixing the primitive-obsession heuristic minimums.
///
/// decision: these primitive-obsession thresholds are detector heuristics, not published or
/// user-tunable metric values, so they stay as named constants at the top of the module rather
/// than in Core.Config, keeping the rationale visible next to the module's other declarations.
let private minDistinctValues = 3
let private sampleSize = 4

type private TypedParameterNode =
    { Name: string
      Type: string
      Node: Node
      KeywordOnly: bool }

/// Detects adjacent, identically typed primitive parameters that callers can accidentally transpose.
///
/// decision: suppresses a pair only when both parameters occur after a language-level keyword-only
/// boundary — optional named call syntax cannot prevent a later positional call from swapping values.
let private findParameterCollisions (paramsNode: Node) (positions: PositionLookup) (language: LanguageAdapter) =
    let _, typed =
        nodeChildren paramsNode
        |> List.fold
            (fun (keywordOnly, typed) node ->
                if List.contains (nodeType node) language.KeywordOnlyBoundaryTypes then
                    true, typed
                else
                    match language.ExtractTypedParameter node with
                    | Some parameter ->
                        keywordOnly,
                        typed
                        @ [ { Name = parameter.Name
                              Type = parameter.Type
                              Node = node
                              KeywordOnly = keywordOnly } ]
                    | None -> keywordOnly, typed)
            (false, [])

    typed
    |> List.pairwise
    |> List.choose (fun (first, second) ->
        if
            first.Type <> second.Type
            || not (Set.contains first.Type language.PrimitiveTypeNames)
            || (first.KeywordOnly && second.KeywordOnly)
        then
            None
        else
            let position = positions.toPosition (nodeStartIndex first.Node)

            Some
                { Line = position.Line
                  Column = position.Column
                  Type = PrimitiveObsession
                  Severity = Medium
                  Message =
                    sprintf
                        "Primitive obsession: consecutive parameters '%s: %s' and '%s: %s' share the same primitive type — a caller can swap them and nothing will complain. Consider %s so the type checker catches it."
                        first.Name
                        first.Type
                        second.Name
                        second.Type
                        language.DistinctTypeAdvice
                  Hotspots = [] })

let private stripQuotes (text: string) = text.Substring(1, text.Length - 2)

/// Detects one function-local variable being compared to three or more distinct string literals.
///
/// assumption: a variable name belongs only to its containing function for this analysis; names reused
/// in unrelated functions must not accumulate into one finding.
let private findStringlyTypedControlFlow (functionNode: Node) (positions: PositionLookup) (language: LanguageAdapter) =
    let isStringLiteral node =
        language.NodeTypes.StringLiteral
        |> Option.exists (fun stringLiteralType -> nodeType node = stringLiteralType)

    let record (variable: Node) (values: string list) state =
        let key = nodeText variable

        match Map.tryFind key state with
        | Some(existingValues, firstOccurrence) ->
            Map.add key (Set.union existingValues (Set.ofList values), firstOccurrence) state
        | None -> Map.add key (Set.ofList values, variable) state

    let rec traverse (node: Node) state =
        let withEqualities =
            language.GetEqualityComparisons node
            |> List.fold
                (fun acc comparison ->
                    if
                        List.contains (nodeType comparison.Left) language.VariableReferenceNodeTypes
                        && isStringLiteral comparison.Right
                    then
                        record comparison.Left [ stripQuotes (nodeText comparison.Right) ] acc
                    elif
                        List.contains (nodeType comparison.Right) language.VariableReferenceNodeTypes
                        && isStringLiteral comparison.Left
                    then
                        record comparison.Right [ stripQuotes (nodeText comparison.Left) ] acc
                    else
                        acc)
                state

        let withMembership =
            language.GetMembershipComparisons node
            |> List.fold
                (fun acc comparison ->
                    if
                        List.contains (nodeType comparison.Left) language.VariableReferenceNodeTypes
                        && not comparison.Values.IsEmpty
                    then
                        record comparison.Left comparison.Values acc
                    else
                        acc)
                withEqualities

        // decision: also counts the idiomatic match/switch dispatch on string literals (F#'s
        // `match x with | "a" -> ... | "b" -> ...`), which the infix-`=` hooks above don't see. The hook
        // returns the scrutinee variable and the string-literal case nodes; the values are accumulated
        // under the same variable key as an equality comparison, so a match and an if/elif chain on the
        // same name in one function still sum toward the threshold.
        let withMatchCases =
            match language.GetMatchStringCases node with
            | Some(scrutinee, caseNodes) ->
                record scrutinee (caseNodes |> List.map (stripQuotes << nodeText)) withMembership
            | None -> withMembership

        nodeChildren node
        |> List.fold (fun acc child -> traverse child acc) withMatchCases

    traverse functionNode Map.empty
    |> Map.toList
    |> List.choose (fun (name, (values, firstOccurrence)) ->
        if Set.count values < minDistinctValues then
            None
        else
            let position = positions.toPosition (nodeStartIndex firstOccurrence)
            let sample = values |> Set.toList |> List.truncate sampleSize
            let suffix = if Set.count values > sample.Length then ", …" else ""

            Some
                { Line = position.Line
                  Column = position.Column
                  Type = PrimitiveObsession
                  Severity = Low
                  Message =
                    sprintf
                        "Stringly-typed control flow: '%s' is compared against %d distinct string literals (%s%s). Consider an Enum or Literal type to catch typos and get exhaustiveness checking."
                        name
                        (Set.count values)
                        (String.concat ", " sample)
                        suffix
                  Hotspots = [] })

/// The "Primitive Obsession" detector identifies primitives being used as unvalidated domain types.
/// Its language-specific parsing knowledge stays in LanguageAdapter so this traversal is shared.
let analyzePrimitiveObsession (ctx: AnalysisContext) : AnalysisContext =
    let rec traverse (node: Node) : EnergyViolation list =
        // decision: analyzes each logical head of a definition (F#'s `and`-binding splits into one head
        // per mutually recursive let) rather than the merged node. This fixes two false results at once:
        // each head's parameters are analyzed for swap risk (previously only the first head's were), and
        // each head's body is scanned for stringly-typed control flow in isolation (previously the merged
        // node let two heads' same-named-variable comparisons accumulate into one phantom finding).
        // For a single-head definition this is one head, so behavior is unchanged.
        let ownViolations =
            if ctx.Language.IsFunctionDefinition node then
                ctx.Language.GetFunctionHeads node
                |> List.collect (fun head ->
                    let parameterViolations =
                        match findParametersNode head.ParametersRoot ctx.Language.NodeTypes.Parameters with
                        | Some parameters -> findParameterCollisions parameters ctx.Positions ctx.Language
                        | None -> []

                    parameterViolations
                    @ findStringlyTypedControlFlow head.Body ctx.Positions ctx.Language)
            else
                []

        ownViolations @ (nodeChildren node |> List.collect traverse)

    let findings = traverse ctx.Tree
    addViolations findings ctx

let detector: Detector =
    { Name = "primitiveObsession"
      Run = analyzePrimitiveObsession }
