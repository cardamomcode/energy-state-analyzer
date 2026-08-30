module Energy.Core.Violation

// Shared violation model for the detector pipeline (port of src/types.ts).
//
// decision: discriminated unions replace the string-literal unions of types.ts — the detectors
// pattern-match on Severity/ViolationType instead of comparing wire strings, which deletes the
// `as any` casts and makes an unknown type/severity a compile error rather than a runtime miss.
// The CLI's JSON contract (src/cli.ts) maps these DUs back to the same wire strings in Phase 2;
// for now the detectors only ever produce F# values, so no mapping exists here yet.

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
    | Suppression -> "suppression"

let severityName =
    function
    | Low -> "low"
    | Medium -> "medium"
    | High -> "high"
