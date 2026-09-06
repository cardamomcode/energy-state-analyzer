module Energy.Core.Violation

// Shared violation model for the detector pipeline.
//
// decision: discriminated unions replace the string-literal unions of types.ts — the detectors
// pattern-match on Severity/ViolationType instead of comparing wire strings, which deletes the
// `as any` casts and makes an unknown type/severity a compile error rather than a runtime miss.
// The CLI's JSON contract maps these DUs back to the established wire strings.

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
    // Error handling that occupies so much of a function's body it shadows the business logic it
    // wraps — a separation-of-concerns/cohesion signal, distinct from cyclomatic/cognitive complexity.
    | ErrorShadowing
    | Suppression

// decision: per-line weighted hotspots (nesting depth for cognitive, decision density for
// cyclomatic) alongside the flat complexity score — lets callers paint a progressive heatmap
// across the function body instead of a single flat highlight, so the worst lines stand out.
type Hotspot = { Line: int; Weight: int }

type EnergyViolation =
    { Line: int
      Column: int
      Type: ViolationType
      Severity: Severity
      Message: string
      // list, not array — no Option/empty-array ceremony (the coherence detector itself flags
      // the latter); a detector that emits no hotspots just passes [].
      Hotspots: Hotspot list }

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
    | ErrorShadowing -> "error-shadowing"
    | Suppression -> "suppression"

/// Stable, user-facing identifiers for analyzer rules.
// decision: rule IDs are opaque, sequential public identifiers rather than derived display names,
// so renaming a detector never breaks SARIF baselines, VS Code links, or documentation references.
let violationRuleId =
    function
    | Nesting -> "ESA-001"
    | Complexity -> "ESA-002"
    | Cognitive -> "ESA-003"
    | Naming -> "ESA-004"
    | Coherence -> "ESA-005"
    | Magic -> "ESA-006"
    | Parameters -> "ESA-007"
    | Inversion -> "ESA-008"
    | PrimitiveObsession -> "ESA-009"
    | MatchOpportunity -> "ESA-010"
    | LogicalControlFlow -> "ESA-011"
    | OpaqueBoolean -> "ESA-012"
    | ErrorShadowing -> "ESA-013"
    | Suppression -> "ESA-014"

let severityName =
    function
    | Low -> "low"
    | Medium -> "medium"
    | High -> "high"
