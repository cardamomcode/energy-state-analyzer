module Energy.Core.Suppressions

open System.Text.RegularExpressions
open Energy.Core.Violation

// decision: these are the capture-group indices of directivePattern (group 1 = comment marker,
// group 2 = optional `-file`, group 3 = suppression types); named constants make the indexed reads
// below self-documenting instead of bare magic numbers. They sit at the top of the module so this
// choice is visible rather than hidden next to their use sites.
let private scopeGroupIndex = 2
let private typeGroupIndex = 3

type SuppressionScope =
    | Line
    | File

type Suppression =
    { Line: int
      Column: int
      Scope: SuppressionScope
      Types: Set<ViolationType> option
      UnknownTypes: string list
      Standalone: bool }

type ApplySuppressionsResult =
    { Violations: EnergyViolation list
      SuppressionNotes: EnergyViolation list }

let private directivePattern =
    Regex("(//|#)\\s*esa-ignore(-file)?(?::\\s*([\\w,\\s-]+))?\\s*$")

// decision: directive type names mirror `violationTypeName` (the JSON/report contract) rather than
// the detector's internal `Name`, so a user copies the exact string from any report. Multi-word
// types are kebab-case (`primitive-obsession`), matching docs and CLI output.
let private knownTypes =
    [ "nesting", Nesting
      "complexity", Complexity
      "cognitive", Cognitive
      "naming", Naming
      "coherence", Coherence
      "magic", Magic
      "parameters", Parameters
      "inversion", Inversion
      "primitive-obsession", PrimitiveObsession
      "match-opportunity", MatchOpportunity
      "logical-control-flow", LogicalControlFlow
      "opaque-boolean", OpaqueBoolean
      "suppression", Suppression ]
    |> Map.ofList

// Parses a type list conservatively: a bare directive means every type, but a list containing only
// misspellings means no type at all and therefore cannot accidentally suppress unrelated findings.
let private parseTypeList (raw: string) =
    if System.String.IsNullOrWhiteSpace raw then
        None, []
    else
        let tokens =
            raw.Split(',')
            |> Array.map _.Trim()
            |> Array.filter (fun token -> token <> "")
            |> Array.toList

        let types, unknownTypes =
            tokens
            |> List.fold
                (fun (types, unknownTypes) token ->
                    match Map.tryFind token knownTypes with
                    | Some violationType -> Set.add violationType types, unknownTypes
                    | None -> types, unknownTypes @ [ token ])
                (Set.empty, [])

        Some types, unknownTypes

// decision: scans source text rather than AST comment nodes because the directive marker is identical
// across languages while each tree-sitter grammar represents comments differently.
let parseSuppressions (sourceText: string) : Suppression list =
    sourceText.Split('\n')
    |> Array.mapi (fun line lineText ->
        let matched = directivePattern.Match lineText

        if not matched.Success then
            None
        else
            let types, unknownTypes = parseTypeList matched.Groups.[typeGroupIndex].Value

            Some
                { Line = line
                  Column = matched.Index
                  Scope =
                    if matched.Groups.[scopeGroupIndex].Success then
                        File
                    else
                        Line
                  Types = types
                  UnknownTypes = unknownTypes
                  Standalone = lineText.Substring(0, matched.Index).Trim() = "" })
    |> Array.choose id
    |> Array.toList

let private coversLine suppression violationLine =
    match suppression.Scope with
    | File -> true
    | Line ->
        violationLine = suppression.Line
        || (suppression.Standalone && violationLine = suppression.Line + 1)

let private matchesType suppression violationType =
    suppression.Types |> Option.forall (Set.contains violationType)

// decision: emits a low-severity finding for unknown or unused directives so suppression debt is
// visible; filtering a violation is never allowed to turn a stale comment into a silent no-op.
let applySuppressions (violations: EnergyViolation list) (sourceText: string) : ApplySuppressionsResult =
    let suppressions = parseSuppressions sourceText |> List.indexed

    if suppressions.IsEmpty then
        { Violations = violations
          SuppressionNotes = [] }
    else
        let remaining, suppressedCounts =
            violations
            |> List.fold
                (fun (remaining, counts) violation ->
                    match
                        suppressions
                        |> List.tryFind (fun (_, suppression) ->
                            coversLine suppression violation.Line && matchesType suppression violation.Type)
                    with
                    | Some(index, _) -> remaining, Map.change index (Option.defaultValue 0 >> (+) 1 >> Some) counts
                    | None -> violation :: remaining, counts)
                ([], Map.empty)

        let notes =
            suppressions
            |> List.collect (fun (index, suppression) ->
                let unknownNote =
                    if suppression.UnknownTypes.IsEmpty then
                        []
                    else
                        [ { Line = suppression.Line
                            Column = suppression.Column
                            Type = Suppression
                            Severity = Low
                            Message =
                              sprintf
                                  "esa-ignore names unknown violation type(s): %s."
                                  (System.String.Join(", ", suppression.UnknownTypes))
                            Hotspots = [] } ]

                let unusedNote =
                    if Map.tryFind index suppressedCounts |> Option.defaultValue 0 > 0 then
                        []
                    else
                        let scopeText = if suppression.Scope = File then "file-wide " else ""

                        [ { Line = suppression.Line
                            Column = suppression.Column
                            Type = Suppression
                            Severity = Low
                            Message =
                              sprintf
                                  "Unused %sesa-ignore — no matching violation found. Remove it or fix the type list."
                                  scopeText
                            Hotspots = [] } ]

                unknownNote @ unusedNote)

        { Violations = List.rev remaining
          SuppressionNotes = notes }
