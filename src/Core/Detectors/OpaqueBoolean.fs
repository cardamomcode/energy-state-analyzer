module Energy.Core.Detectors.OpaqueBoolean

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

let analyzeOpaqueBooleanLiteral (ctx: AnalysisContext) : AnalysisContext =
    let rec walk node =
        let own =
            if ctx.Language.IsBooleanLiteral node && ctx.Language.IsPositionalCallArgument node then
                let position = ctx.Positions.toPosition (nodeStartIndex node)

                [ { Line = position.Line
                    Column = position.Column
                    Type = OpaqueBoolean
                    Severity = Low
                    Message =
                      sprintf
                          "Opaque boolean literal: a bare '%s' passed positionally tells the reader nothing without checking the callee's signature. Name it at the call site (a keyword argument, an object-literal field, or F#'s named-argument syntax) — or better, split into two clearly named functions (e.g. enableX()/disableX()) or use an enum."
                          (nodeText node)
                    Hotspots = [] } ]
            else
                []

        own @ (nodeChildren node |> List.collect walk)

    let findings = walk ctx.Tree
    addViolations findings ctx

let detector: Detector =
    { Name = "opaqueBoolean"
      Run = analyzeOpaqueBooleanLiteral }
