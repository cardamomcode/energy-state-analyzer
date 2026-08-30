module Energy.Core.Context

open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter

// The shared immutable analysis context + the detector abstraction (port of the parameter
// quadruple in src/core/analyze.ts, and §3.3).
//
// decision: one immutable context replaces the repeated (tree, positions, language, fileName)
// quadruple — which is exactly the swap-risk shape this repo's own primitive-obsession detector
// flags. It carries the per-document-varying inputs; each detector owns its own threshold record
// and default (see Detectors/*), so it is passed explicitly to the detector rather than living on
// the context. That keeps this module free of a cross-dependency on the detector modules while
// still giving every detector a pure `AnalysisContext -> EnergyViolation list` entry point via its
// `detector` value below.

type AnalysisContext =
    { Source: string
      // Root node of the already-parsed tree — the pipeline is synchronous, so no task { } here.
      Tree: Node
      // Offset->line/column lookup derived from Source; detectors map node.startIndex through it.
      Positions: PositionLookup
      Language: LanguageAdapter
      FileName: string }

// A named detector pass over the shared context. The pipeline (Analyze.fs) is a list of these, so
// adding a detector is one line — the "adding a new detector" rule in AGENTS.md becomes literally that.
type Detector = { Name: string; Run: AnalysisContext -> EnergyViolation list }
