module Energy.Core.ReportHuman

open System.Text.RegularExpressions
open Energy.Core.Report
open Energy.Core.Violation

/// Named constants fixing the report's score scale and risk-score boundaries.
///
/// decision: the score scale and risk boundaries are fixed parts of the published report/diff
/// metric rather than tunable detector thresholds, so they live as named constants at the top of
/// the module (not in Core.Config) instead of being hidden next to their use sites. The upper
/// complexity bound stays an int to match extractComplexityValue's return type.
let private maxComplexityScore = 100
let private scoreCeiling = 10.0
let private lowThreshold = 4.0
let private mediumThreshold = 7.0
let private highThreshold = 9.0
let private highFileScore = 7.5
let private mediumFileScore = 5.0

type RiskLevel =
    | NoRisk
    | LowRisk
    | MediumRisk
    | HighRisk
    | Critical

let private complexityCurve = [ 0, 0.0; 10, 3.9; 20, 6.9; 50, 8.9; 100, 10.0 ]

let complexityToScore value =
    if value <= 0 then
        0.0
    elif value >= maxComplexityScore then
        scoreCeiling
    else
        complexityCurve
        |> List.pairwise
        |> List.find (fun ((_, _), (next, _)) -> value <= next)
        |> fun ((previousValue, previousScore), (nextValue, nextScore)) ->
            let ratio = float (value - previousValue) / float (nextValue - previousValue)
            System.Math.Round(previousScore + ratio * (nextScore - previousScore), 1)

let classifyScore score =
    if score <= 0.0 then NoRisk
    elif score < lowThreshold then LowRisk
    elif score < mediumThreshold then MediumRisk
    elif score < highThreshold then HighRisk
    else Critical

let classifyComplexityScore value =
    complexityToScore value |> classifyScore

let private riskLabel =
    function
    | NoRisk -> "None"
    | LowRisk -> "Low"
    | MediumRisk -> "Medium"
    | HighRisk -> "High"
    | Critical -> "Critical"

/// Describe the report band as review priority without inferring testability.
let private riskDescription =
    function
    | NoRisk -> "no violations found"
    | LowRisk -> "lower review priority"
    | MediumRisk -> "moderate review priority"
    | HighRisk -> "high review priority"
    | Critical -> "highest review priority"

let private categoryLabel =
    function
    | Nesting -> "Nesting depth"
    | Complexity -> "Cyclomatic complexity"
    | Cognitive -> "Cognitive complexity"
    | Naming -> "Naming"
    | Coherence -> "File coherence"
    | Magic -> "Magic values"
    | Parameters -> "Parameter count"
    | Inversion -> "Inversion opportunities"
    | PrimitiveObsession -> "Primitive obsession"
    | MatchOpportunity -> "Match opportunities"
    | LogicalControlFlow -> "Logical operator as control flow"
    | OpaqueBoolean -> "Opaque boolean literals"
    | ErrorShadowing -> "Error handling shadows logic"
    | ParseDontValidate -> "Parse, don't validate"
    | Suppression -> "Suppression directives"

/// Explain the reading burden behind each reported category.
let private categoryBlurb =
    function
    | Nesting ->
        Some
            "control-flow blocks nested deep enough that a reader has to hold several levels of context in mind at once"
    | Naming -> Some "naming that obscures intent"
    | Coherence ->
        Some
            "the file mixes too many responsibilities (too many functions/imports, or too many large functions) to read as one coherent unit"
    | Magic -> Some "unnamed literals standing in for a value that deserves a name"
    | Parameters -> Some "a function with enough parameters that call sites are easy to get wrong"
    | Inversion ->
        Some
            "terminal conditionals or deep if-nesting that may be easier to read with guard clauses or a named operation"
    | PrimitiveObsession -> Some "adjacent same-typed values a caller could silently swap without the compiler noticing"
    | MatchOpportunity -> Some "an if/elif chain on one variable that would read more clearly as a match/switch"
    | LogicalControlFlow -> Some "&&/|| used to hide an if statement"
    | OpaqueBoolean -> Some "a bare true/false at a call site that only makes sense by reading the callee"
    | ErrorShadowing -> Some "an overly broad protected try region or recovery/cleanup policy that dominates a function"
    | ParseDontValidate -> Some "a checked property that is not preserved in the returned type"
    | Suppression ->
        Some "an esa-ignore comment that names an unknown violation type, or no longer matches any violation"
    | Complexity
    | Cognitive -> None

/// Extract the numeric complexity value from a violation's detector message.
///
/// decision: derives the complexity value from the established detector message rather than
/// changing the public violation shape solely for a report-only view.
let private extractComplexityValue violation =
    match violation.Type with
    | Complexity
    | Cognitive ->
        let matched =
            Regex.Match(violation.Message, "complexity: (\\d+)", RegexOptions.IgnoreCase)

        if matched.Success then
            Some(int matched.Groups.[1].Value)
        else
            None
    | _ -> None

let private describeComplexityFindings label violations =
    let values = violations |> List.choose extractComplexityValue |> List.sortDescending

    match values with
    | [] -> None
    | worst :: _ ->
        let score = complexityToScore worst
        let level = classifyScore score

        let countText =
            if values.Length = 1 then
                sprintf "1 function scores %d" worst
            else
                sprintf
                    "%d functions score %s (worst: %d)"
                    values.Length
                    (values |> List.map (string<int>) |> String.concat ", ")
                    worst

        Some(
            sprintf
                "- **%s**: %s — score %.1f (%s): %s."
                label
                countText
                score
                (riskLabel level)
                (riskDescription level)
        )

let private describeCategoryFindings violationType violations =
    match violationType with
    | Complexity
    | Cognitive -> describeComplexityFindings (categoryLabel violationType) violations
    | _ ->
        let counts =
            violations
            |> List.fold
                (fun state violation ->
                    match violation.Severity with
                    | High -> state @ [ "high" ]
                    | Medium -> state @ [ "medium" ]
                    | Low -> state @ [ "low" ])
                []

        let severityText =
            [ "high"; "medium"; "low" ]
            |> List.choose (fun severity ->
                let count = counts |> List.filter ((=) severity) |> List.length in

                if count = 0 then
                    None
                else
                    Some(sprintf "%d %s" count severity))
            |> String.concat ", "

        let suffix =
            categoryBlurb violationType
            |> Option.map (fun blurb -> " — " + blurb)
            |> Option.defaultValue ""

        let plural = if violations.Length = 1 then "" else "s"

        Some(
            sprintf
                "- **%s**: %d finding%s (%s)%s."
                (categoryLabel violationType)
                violations.Length
                plural
                severityText
                suffix
        )

/// Compute a single 0.0–10.0 risk score from one file's violations.
///
/// invariant: non-complexity findings never produce Critical; that level remains reserved for
/// an extreme cyclomatic or cognitive score.
let private fileScore violations =
    match violations |> List.choose extractComplexityValue |> List.sortDescending with
    | value :: _ -> complexityToScore value
    | [] when violations |> List.exists (fun violation -> violation.Severity = High) -> highFileScore
    | [] when violations |> List.exists (fun violation -> violation.Severity = Medium) -> mediumFileScore
    | [] when not violations.IsEmpty -> 2.0
    | [] -> 0.0

let private renderFileSection result =
    let score = fileScore result.Violations

    let sections =
        result.Violations
        |> List.groupBy _.Type
        |> List.choose (fun (violationType, violations) -> describeCategoryFindings violationType violations)

    [ sprintf "## %s — %s (score %.1f)" result.FilePath (riskLabel (classifyScore score)) score
      "" ]
    @ sections
    |> String.concat "\n"

/// Explain the report scale and its limits as a review heuristic.
let private scoreLegend =
    [ "## Score legend"
      ""
      "_The 0.0–10.0 report score prioritizes review using None/Low/Medium/High/Critical labels. It does not measure defect probability, testability, or overall code quality._"
      ""
      "| Score | Risk label | Review priority | Cyclomatic/cognitive input |"
      "| --- | --- | --- | --- |"
      "| 0.0 | None | No violations found | — |"
      "| 0.1–3.9 | Low | Lower review priority | 1–10 |"
      "| 4.0–6.9 | Medium | Moderate review priority | 11–20 |"
      "| 7.0–8.9 | High | High review priority | 21–50 |"
      "| 9.0–10.0 | Critical | Highest review priority | 50+ |"
      ""
      "_Cyclomatic complexity describes independent control-flow paths; cognitive complexity estimates reading effort. The report applies the same numeric curve to both as a prioritization heuristic, not evidence that they measure equivalent effort. A file with no complexity violations instead gets a fixed score from its worst other finding (Low 2.0 / Medium 5.0 / High 7.5)._" ]
    |> String.concat "\n"

/// Render findings and review priorities as a human-readable Markdown report.
let renderHumanReport results =
    let flagged =
        results
        |> List.filter (fun result -> not result.Violations.IsEmpty)
        |> List.sortByDescending (fun result -> fileScore result.Violations)

    let noFindingsCount = results.Length - flagged.Length

    let fileScores =
        results |> List.map (fun result -> result.FilePath, fileScore result.Violations)

    let worst =
        fileScores
        |> List.fold (fun winner candidate -> if snd candidate > snd winner then candidate else winner) ("", 0.0)

    let riskCounts = fileScores |> List.countBy (snd >> classifyScore) |> Map.ofList

    let totalCounts =
        results
        |> List.collect _.Violations
        |> List.fold
            (fun counts violation ->
                match violation.Severity with
                | Low -> { counts with Low = counts.Low + 1 }
                | Medium ->
                    { counts with
                        Medium = counts.Medium + 1 }
                | High -> { counts with High = counts.High + 1 })
            emptyCounts

    let repoLine =
        if snd worst > 0.0 then
            sprintf
                "**Repo score: %.1f (%s)** — driven by the worst file in the scan, `%s` (%s)."
                (snd worst)
                (riskLabel (classifyScore (snd worst)))
                (fst worst)
                (riskDescription (classifyScore (snd worst)))
        else
            "**Repo score: 0.0 (None)** — no violations were found anywhere in the scan."

    let filesSuffix = if results.Length = 1 then "" else "s"
    let total = totalCounts.Low + totalCounts.Medium + totalCounts.High
    let findingSuffix = if total = 1 then "" else "s"

    [ "# Energy State Report"
      ""
      scoreLegend
      ""
      sprintf
          "**%d file%s scanned** — %d with no findings, %d flagged"
          results.Length
          filesSuffix
          noFindingsCount
          flagged.Length
      "" ]
    @ (flagged |> List.collect (fun result -> [ renderFileSection result; "" ]))
    @ [ "## Total evaluation"
        ""
        repoLine
        ""
        "This is the _maximum_ file score, not an average across files — see the note at the top of this report for why an average would hide the file most worth fixing."
        ""
        "| Risk | Files |"
        "| --- | --- |"
        sprintf "| None | %d |" (Map.tryFind NoRisk riskCounts |> Option.defaultValue 0)
        sprintf "| Low | %d |" (Map.tryFind LowRisk riskCounts |> Option.defaultValue 0)
        sprintf "| Medium | %d |" (Map.tryFind MediumRisk riskCounts |> Option.defaultValue 0)
        sprintf "| High | %d |" (Map.tryFind HighRisk riskCounts |> Option.defaultValue 0)
        sprintf "| Critical | %d |" (Map.tryFind Critical riskCounts |> Option.defaultValue 0)
        ""
        sprintf
            "**%d total finding%s** (%d high, %d medium, %d low) — breadth of issues across the scan, independent of peak severity."
            total
            findingSuffix
            totalCounts.High
            totalCounts.Medium
            totalCounts.Low ]
    |> String.concat "\n"
