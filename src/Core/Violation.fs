module Energy.Core.Violation

/// Shared violation model for the detector pipeline.
///
/// decision: discriminated unions replace the string-literal unions of types.ts — the detectors
/// pattern-match on Severity/ViolationType instead of comparing wire strings, which deletes the
/// `as any` casts and makes an unknown type/severity a compile error rather than a runtime miss.
/// The CLI's JSON contract maps these DUs back to the established wire strings.

type Severity =
    | Low
    | Medium
    | High

type ViolationType =
    | Nesting
    | Complexity
    | Cognitive
    | Naming
    | Coherence
    | Magic
    | Parameters
    | Inversion
    | PrimitiveObsession
    | MatchOpportunity
    | LogicalControlFlow
    | OpaqueBoolean
    // Independent exception-boundary breadth, recovery-share, and recovery-body size signals.
    | ErrorShadowing
    | RecoveryDominance
    | OversizedRecoveryBlock
    | Suppression
    | ParseDontValidate

/// A single source line's severity weight, used to paint a progressive heatmap across a function body.
///
/// decision: per-line weighted hotspots (nesting depth for cognitive, decision density for
/// cyclomatic) alongside the flat complexity score — lets callers paint a progressive heatmap
/// across the function body instead of a single flat highlight, so the worst lines stand out.
type Hotspot = { Line: int; Weight: int }

type EnergyViolation =
    {
        Line: int
        Column: int
        Type: ViolationType
        Severity: Severity
        Message: string
        // list, not array — no Option/empty-array ceremony (the coherence detector itself flags
        // the latter); a detector that emits no hotspots just passes [].
        Hotspots: Hotspot list
    }

/// Stable JSON/report names retained from the public TypeScript CLI contract.
let violationTypeName =
    function
    | Nesting -> "nesting"
    | Complexity -> "complexity"
    | Cognitive -> "cognitive"
    | Naming -> "naming"
    | Coherence -> "coherence"
    | Magic -> "magic"
    | Parameters -> "parameters"
    | Inversion -> "inversion"
    | PrimitiveObsession -> "primitive-obsession"
    | MatchOpportunity -> "match-opportunity"
    | LogicalControlFlow -> "logical-control-flow"
    | OpaqueBoolean -> "opaque-boolean"
    | RecoveryDominance -> "recovery-dominance"
    | OversizedRecoveryBlock -> "oversized-recovery-block"
    | ErrorShadowing -> "error-shadowing"
    | ParseDontValidate -> "parse-dont-validate"
    | Suppression -> "suppression"

/// Pascal-case rule name for SARIF's tool.driver.rules[].name, kept distinct from
/// violationTypeName's kebab-case identifier since that one is a public JSON/esa-ignore contract.
let violationRuleName =
    function
    | Nesting -> "Nesting"
    | Complexity -> "Complexity"
    | Cognitive -> "Cognitive"
    | Naming -> "Naming"
    | Coherence -> "Coherence"
    | Magic -> "Magic"
    | Parameters -> "Parameters"
    | Inversion -> "Inversion"
    | PrimitiveObsession -> "PrimitiveObsession"
    | MatchOpportunity -> "MatchOpportunity"
    | LogicalControlFlow -> "LogicalControlFlow"
    | OpaqueBoolean -> "OpaqueBoolean"
    | ErrorShadowing -> "ErrorShadowing"
    | RecoveryDominance -> "RecoveryDominance"
    | OversizedRecoveryBlock -> "OversizedRecoveryBlock"
    | ParseDontValidate -> "ParseDontValidate"
    | Suppression -> "Suppression"

/// Display error-boundary rule names while preserving their public wire identities.
let violationDisplayName kind =
    match kind with
    | ErrorShadowing -> "Broad Protected Scope"
    | RecoveryDominance -> "Recovery Dominance"
    | OversizedRecoveryBlock -> "Oversized Recovery Block"
    | _ -> violationTypeName kind

/// Stable, user-facing identifiers for analyzer rules.
/// decision: rule IDs are opaque, sequential public identifiers rather than derived display names,
/// so renaming a detector never breaks SARIF baselines, VS Code links, or documentation references.
let violationRuleId =
    function
    | Nesting -> "ESA001"
    | Complexity -> "ESA002"
    | Cognitive -> "ESA003"
    | Naming -> "ESA004"
    | Coherence -> "ESA005"
    | Magic -> "ESA006"
    | Parameters -> "ESA007"
    | Inversion -> "ESA008"
    | PrimitiveObsession -> "ESA009"
    | MatchOpportunity -> "ESA010"
    | LogicalControlFlow -> "ESA011"
    | OpaqueBoolean -> "ESA012"
    | ErrorShadowing -> "ESA013"
    | Suppression -> "ESA014"
    | ParseDontValidate -> "ESA015"
    | RecoveryDominance -> "ESA016"
    | OversizedRecoveryBlock -> "ESA017"

/// Canonical documentation for each user-facing analyzer rule.
/// decision: keeps SARIF help links beside stable rule identifiers so a detector rename or report
/// renderer change cannot silently send users to the generic detector index.
let violationHelpUri =
    function
    | Nesting -> "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/excessive-nesting.md"
    | Complexity ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/cyclomatic-complexity.md"
    | Cognitive ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/cognitive-complexity.md"
    | Naming -> "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/file-coherence.md"
    | Coherence -> "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/file-coherence.md"
    | Magic -> "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/magic-values.md"
    | Parameters ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/parameter-explosion.md"
    | Inversion ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/inversion-opportunities.md"
    | PrimitiveObsession ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/primitive-obsession.md"
    | MatchOpportunity ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/match-opportunities.md"
    | LogicalControlFlow ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/logical-operator-control-flow.md"
    | OpaqueBoolean ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/opaque-boolean-literal.md"
    | RecoveryDominance ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/recovery-dominance.md"
    | OversizedRecoveryBlock ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/oversized-recovery-block.md"
    | ErrorShadowing ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/error-shadowing.md"
    | ParseDontValidate ->
        "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/parse-dont-validate.md"
    | Suppression -> "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors/suppression.md"

let severityName =
    function
    | Low -> "low"
    | Medium -> "medium"
    | High -> "high"
