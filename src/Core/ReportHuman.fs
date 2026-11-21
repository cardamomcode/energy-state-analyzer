// # ReportHuman
//
// The human report explains findings and prioritizes the worst affected files. Recognized complexity
// messages map through a bounded score curve; files without such values use a severity fallback.
// Repository risk is the maximum file score, while finding counts describe breadth. This view does not
// replace Report.fs additive scores or prove testing effort.
//
// Reading path: [Source guide](../../docs/source-guide.md).

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
/// Maximum score on the human report's zero-to-ten prioritization scale.
let private scoreCeiling = 10.0
/// Exclusive upper boundary of the Low risk band.
let private lowThreshold = 4.0
/// Exclusive upper boundary of the Medium risk band.
let private mediumThreshold = 7.0
/// Exclusive upper boundary of the High risk band; scores at or above it are Critical.
let private highThreshold = 9.0
/// Fallback file score when no complexity value is available and at least one finding is high severity.
let private highFileScore = 7.5
/// Fallback file score when no complexity value is available and the worst finding is medium severity.
let private mediumFileScore = 5.0

/// Human-report risk bands, including NoRisk for zero and Critical for extreme complexity; these are
/// distinct from detector severities.
type RiskLevel =
    | NoRisk
    | LowRisk
    | MediumRisk
    | HighRisk
    | Critical

/// Piecewise-linear anchors mapping raw complexity counts to the human report's score scale.
let private complexityCurve = [ 0, 0.0; 10, 3.9; 20, 6.9; 50, 8.9; 100, 10.0 ]

/// Map a complexity count onto the piecewise-linear score curve, rounding to one decimal and clamping
/// nonpositive counts to zero and counts of at least 100 to ten.
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

/// Classify a score using the fixed risk-band boundaries; nonpositive scores have no risk.
let classifyScore score =
    if score <= 0.0 then NoRisk
    elif score < lowThreshold then LowRisk
    elif score < mediumThreshold then MediumRisk
    elif score < highThreshold then HighRisk
    else Critical

/// Convert a raw complexity count to the report score and classify that rounded score.
let classifyComplexityScore value =
    complexityToScore value |> classifyScore

/// Render the risk-band name used in file headings and report totals.
let private riskLabel =
    function
    | NoRisk -> "None"
    | LowRisk -> "Low"
    | MediumRisk -> "Medium"
    | HighRisk -> "High"
    | Critical -> "Critical"

/// Explain the complexity burden and the action appropriate to each severity band.
///
/// decision: severity descriptions give agents remediation guidance without treating either
/// complexity metric as proof that code is exhaustively testable or untestable.
let private riskDescription =
    function
    | NoRisk -> "no violations found"
    | LowRisk -> "relatively simple; keep changes small and verify behavior"
    | MediumRisk -> "becoming harder to understand and test; simplify branching or nesting"
    | HighRisk -> "complex and difficult to verify; separate responsibilities and test decisions independently"
    | Critical -> "extremely complex; restructure into smaller, independently understandable and testable units"

/// Render a human category label, including the independent protected-scope and recovery rules.
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
    | ErrorShadowing -> "Broad Protected Scope"
    | RecoveryDominance -> "Recovery Dominance"
    | OversizedRecoveryBlock -> "Oversized Recovery Block"
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
    | ErrorShadowing -> Some "an overly broad protected try region"
    | RecoveryDominance -> Some "recovery/cleanup policy that dominates a function"
    | OversizedRecoveryBlock -> Some "an individual handler or cleanup body exceeding the configured line limit"
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

/// Describe recognized complexity values in descending order and use the worst for risk guidance. Return
/// None when no detector message yields a value.
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

/// Describe complexity using its numeric scores, or summarize another category by severity counts and its
/// reading burden.
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

/// Render a file heading with its risk score and one description per finding category, retaining category
/// encounter order.
let private renderFileSection result =
    let score = fileScore result.Violations

    let sections =
        result.Violations
        |> List.groupBy _.Type
        |> List.choose (fun (violationType, violations) -> describeCategoryFindings violationType violations)

    [
        sprintf "## %s — %s (score %.1f)" result.FilePath (riskLabel (classifyScore score)) score
        ""
    ]
    @ sections
    |> String.concat "\n"

/// Explain the familiar severity bands, remediation guidance, and score calculation.
let private scoreLegend =
    [
        "## Score legend"
        ""
        "_Scores use the familiar [CVSS 0–10 severity bands](https://www.first.org/cvss/v3.1/specification-document#t5) to communicate the seriousness of analyzer findings. These are code complexity and maintainability scores, not security vulnerability scores._"
        ""
        "| Score | Severity | Meaning and action | Cyclomatic/cognitive input |"
        "| --- | --- | --- | --- |"
        "| 0.0 | None | No violations found | — |"
        "| 0.1–3.9 | Low | Relatively simple; keep changes small and verify behavior | 1–10 |"
        "| 4.0–6.9 | Medium | Becoming harder to understand and test; simplify branching or nesting | 11–20 |"
        "| 7.0–8.9 | High | Complex and difficult to verify; separate responsibilities and test decisions independently | 21–50 |"
        "| 9.0–10.0 | Critical | Extremely complex; restructure into smaller, independently understandable and testable units | 50+ |"
        ""
        "_Cyclomatic complexity describes independent control-flow paths; cognitive complexity estimates reading effort. The report applies the same numeric curve to both as a prioritization heuristic, not evidence that they measure equivalent effort. A file with no complexity violations instead gets a fixed score from its worst other finding (Low 2.0 / Medium 5.0 / High 7.5)._"
    ]
    |> String.concat "\n"

/// Render findings, severity, and remediation guidance as a human-readable Markdown report.
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
                        Medium = counts.Medium + 1
                    }
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

    [
        "# Energy State Report"
        ""
        scoreLegend
        ""
        sprintf
            "**%d file%s scanned** — %d with no findings, %d flagged"
            results.Length
            filesSuffix
            noFindingsCount
            flagged.Length
        ""
    ]
    @ (flagged |> List.collect (fun result -> [ renderFileSection result; "" ]))
    @ [
        "## Total evaluation"
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
            totalCounts.Low
    ]
    |> String.concat "\n"
