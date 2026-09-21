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

/// Spellings of the boolean type across the languages this detector runs on: "bool" (Python, F#, C#,
/// C++), "boolean" (TypeScript), "Boolean" (Kotlin). Booleans get their own boolean-blindness check
/// instead of the generic primitive-obsession one, and unlike that adjacency-based check, position
/// doesn't matter: a caller reading `f(true, false)` cannot tell which flag is which even when the
/// types can't be swapped silently, so the fix is a named type for the combination, not just a
/// distinct type per value.
let private booleanTypeNames = Set.ofList [ "bool"; "boolean"; "Boolean" ]

type private TypedParameterNode =
    {
        Name: string
        Type: string
        Node: Node
        KeywordOnly: bool
    }

/// Detects adjacent, identically typed non-boolean primitive parameters that callers can accidentally
/// transpose.
///
/// decision: suppresses a pair only when both parameters occur after a language-level keyword-only
/// boundary — optional named call syntax cannot prevent a later positional call from swapping values.
///
/// decision: booleans are excluded here and evaluated separately in `findBooleanBlindness`, since
/// unlike two same-typed numbers, boolean blindness doesn't depend on adjacency.
let private findSwapRiskViolations
    (typed: TypedParameterNode list)
    (positions: PositionLookup)
    (language: LanguageAdapter)
    =
    typed
    |> List.pairwise
    |> List.filter (fun (first, second) ->
        first.Type = second.Type
        && Set.contains first.Type language.PrimitiveTypeNames
        && not (Set.contains first.Type booleanTypeNames)
        && not (first.KeywordOnly && second.KeywordOnly))
    |> List.map (fun (first, second) ->
        {
            Line = (positions.toPosition (nodeStartIndex first.Node)).Line
            Column = (positions.toPosition (nodeStartIndex first.Node)).Column
            Type = PrimitiveObsession
            Severity = Medium
            Message =
                $"Primitive obsession: consecutive parameters '%s{first.Name}: %s{first.Type}' and '%s{second.Name}: %s{second.Type}' share the same primitive type — a caller can swap them and nothing will complain. Consider %s{language.DistinctTypeAdvice} so the type checker catches it."
            Hotspots = []
        })

/// Detects two or more boolean parameters in a signature, adjacent or not: a non-boolean parameter
/// between two booleans breaks swap-risk adjacency, but a caller reading `f(mode, name, notify)` still
/// cannot tell which flag is which.
///
/// decision: a keyword-only boolean is excluded from the count — its signature already forces every
/// call site to name it, so it isn't blind the way a positional boolean is.
let private findBooleanBlindness (typed: TypedParameterNode list) (positions: PositionLookup) =
    match
        typed
        |> List.filter (fun p -> Set.contains p.Type booleanTypeNames && not p.KeywordOnly)
    with
    | first :: _ :: _ as booleans ->
        let count = booleans.Length
        let names = booleans |> List.map (fun p -> p.Name) |> String.concat ", "
        let suffix = if count > 2 then ", ..." else ""

        Some
            {
                Line = (positions.toPosition (nodeStartIndex first.Node)).Line
                Column = (positions.toPosition (nodeStartIndex first.Node)).Column
                Type = PrimitiveObsession
                Severity = Medium
                Message =
                    $"Boolean blindness: this signature takes %d{count} boolean parameters (%s{names}), adjacent or not — a call site like f(true, false%s{suffix}) doesn't say which flag is which, and combinations invalid in this domain still type-check. Consider a single enum or union naming the valid combinations instead of independent booleans."
                Hotspots = []
            }
    | _ -> None

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
                        @ [
                            {
                                Name = parameter.Name
                                Type = parameter.Type
                                Node = node
                                KeywordOnly = keywordOnly
                            }
                        ]
                    | None -> keywordOnly, typed)
            (false, [])

    findSwapRiskViolations typed positions language
    @ Option.toList (findBooleanBlindness typed positions)

let private stripQuotes (text: string) = text.Substring(1, text.Length - 2)

/// Normalize either orientation of a variable-to-string equality into one aggregation entry.
let private stringEquality (language: LanguageAdapter) (comparison: EqualityComparison) =
    let isVariable node =
        List.contains (nodeType node) language.VariableReferenceNodeTypes

    let isString node =
        language.NodeTypes.StringLiteral |> Option.exists ((=) (nodeType node))

    match
        isVariable comparison.Left, isString comparison.Right, isVariable comparison.Right, isString comparison.Left
    with
    | true, true, _, _ -> Some(comparison.Left, stripQuotes (nodeText comparison.Right))
    | _, _, true, true -> Some(comparison.Right, stripQuotes (nodeText comparison.Left))
    | _ -> None

/// Merge values under their variable name while keeping the first source occurrence.
let private recordStringValues (variable: Node) (values: string list) state =
    let key = nodeText variable

    match Map.tryFind key state with
    | Some(existingValues, firstOccurrence) ->
        Map.add key (Set.union existingValues (Set.ofList values), firstOccurrence) state
    | None -> Map.add key (Set.ofList values, variable) state

/// Detects one function-local variable being compared to three or more distinct string literals.
///
/// assumption: a variable name belongs only to its containing function for this analysis; names reused
/// in unrelated functions must not accumulate into one finding.
let private findStringlyTypedControlFlow (functionNode: Node) (positions: PositionLookup) (language: LanguageAdapter) =
    let rec traverse (node: Node) state =
        let withEqualities =
            language.GetEqualityComparisons node
            |> List.choose (stringEquality language)
            |> List.fold (fun acc (variable, value) -> recordStringValues variable [ value ] acc) state

        let withMembership =
            language.GetMembershipComparisons node
            |> List.fold
                (fun acc comparison ->
                    if
                        List.contains (nodeType comparison.Left) language.VariableReferenceNodeTypes
                        && not comparison.Values.IsEmpty
                    then
                        recordStringValues comparison.Left comparison.Values acc
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
                recordStringValues scrutinee (caseNodes |> List.map (stripQuotes << nodeText)) withMembership
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
                {
                    Line = position.Line
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
                    Hotspots = []
                })

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
    {
        Name = "primitiveObsession"
        Run = analyzePrimitiveObsession
    }
