module Energy.Core.Detectors.ParseDontValidate

open Energy.Core.TreeSitter
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Position
open Energy.Core.Violation
open Energy.Core.Detectors.ParameterCount

/// Collect syntax within one expression or parameter head.
let rec private descendants node =
    node :: (nodeChildren node |> List.collect descendants)

/// Collect parameter names, retaining type annotations where available.
let private parameters language head =
    findParametersNode head.ParametersRoot language.NodeTypes.Parameters
    |> Option.map nodeNamedChildren
    |> Option.defaultValue []
    |> List.map (fun node ->
        match language.ExtractTypedParameter node with
        | Some parameter -> parameter.Name, Some parameter.Type
        | None -> nodeText node, None)

/// Identify a direct comparison that rejects a parameter's null value.
///
/// decision: require a parameter, a null literal, and a null-equality operator as siblings of one
/// expression node — calls such as `value.isNull()` or `isMissing(value, null)` are opaque
/// predicates, not compiler-enforced type narrowing.
let private rejectsNull language name condition =
    let conditionChildren = nodeChildren condition

    let isParameter node =
        nodeText node = name
        && List.contains (nodeType node) language.VariableReferenceNodeTypes

    let isNullLiteral node =
        List.contains (nodeText node) [ "null"; "None"; "nil"; "nullptr" ]

    let isNullEqualityOperator node =
        List.contains (nodeText node) [ "=="; "==="; "is"; "=" ]

    conditionChildren |> List.exists isParameter
    && conditionChildren |> List.exists isNullLiteral
    && conditionChildren |> List.exists isNullEqualityOperator

/// Identify parameters whose checked property is not carried by the success result.
///
/// tradeoff: identity returns still require an annotation; check-only validators also accept simple untyped parameters because they expose no result to refine.
/// decision: the rejectsNull exception stays limited to identity returns, where the returned value
/// itself can preserve language-level narrowing; a bare boolean success never carries the checked
/// value or its narrowing across the call, so plain is_not_null-style boolean validators remain
/// findings in the BareBoolean branch.
let private checkedParameter (language: LanguageAdapter) head candidate =
    let references = descendants candidate.Condition

    let referenced name =
        references
        |> List.exists (fun node ->
            List.contains (nodeType node) language.VariableReferenceNodeTypes
            && nodeText node = name)

    let returnType = language.ExtractReturnType head.ParametersRoot

    parameters language head
    |> List.tryFind (fun (name, parameterType) ->
        referenced name
        && match candidate.Success with
           | NoValue ->
               returnType
               |> Option.forall (fun value -> List.contains value [ "None"; "unit"; "Unit"; "void"; "undefined" ])
           | UnchangedInput value ->
               nodeText value = name
               && List.contains (nodeType value) language.VariableReferenceNodeTypes
               && (parameterType
                   |> Option.exists (fun expected -> returnType |> Option.forall ((=) expected)))
               && not (rejectsNull language name candidate.Condition)
           | BareBoolean _ -> true)

/// Report discarded guard information, leaving domain construction and ordinary computation alone.
let private inspect ctx head =
    ctx.Language.GetGuardedValidation head
    |> Option.bind (fun candidate ->
        checkedParameter ctx.Language head candidate
        |> Option.map (fun (name, _) ->
            let position = ctx.Positions.toPosition (nodeStartIndex candidate.Anchor)

            {
                Line = position.Line
                Column = position.Column
                Type = ParseDontValidate
                Severity = Low
                Message =
                    sprintf
                        "Parse, don't validate: '%s' is checked but %s. Consider returning a domain type that preserves the checked property instead of requiring callers to remember it."
                        name
                        (match candidate.Success with
                         | NoValue -> "success returns no useful value"
                         | UnchangedInput _ -> "returned unchanged"
                         | BareBoolean _ -> "success returns only a boolean")
                Hotspots = []
            }))
    |> Option.toList

/// Walk named functions independently through the registered language adapter.
let analyzeParseDontValidate (ctx: AnalysisContext) : AnalysisContext =
    let rec walk node =
        let own =
            if ctx.Language.IsFunctionDefinition node then
                ctx.Language.GetFunctionHeads node |> List.collect (inspect ctx)
            else
                []

        own @ (nodeNamedChildren node |> List.collect walk)

    addViolations (walk ctx.Tree) ctx

/// Register the information-preservation advisory.
let detector: Detector =
    {
        Name = "parseDontValidate"
        Run = analyzeParseDontValidate
    }
