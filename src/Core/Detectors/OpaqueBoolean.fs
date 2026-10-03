module Energy.Core.Detectors.OpaqueBoolean

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Detectors.TestFile

/// Collect positional boolean literals from an enabled source file.
let private analyzeEnabledOpaqueBooleans (ctx: AnalysisContext) : AnalysisContext =
    let rec walk node =
        let own =
            if ctx.Language.IsBooleanLiteral node && ctx.Language.IsPositionalCallArgument node then
                let position = ctx.Positions.toPosition (nodeStartIndex node)

                [
                    {
                        Line = position.Line
                        Column = position.Column
                        Type = OpaqueBoolean
                        Severity = Low
                        Message =
                            sprintf
                                "Opaque boolean literal: a bare '%s' passed positionally tells the reader nothing without checking the callee's signature. Name it at the call site (a keyword argument, an object-literal field, or F#'s named-argument syntax) — or better, split into two clearly named functions (e.g. enableX()/disableX()) or use an enum."
                                (nodeText node)
                        Hotspots = []
                    }
                ]
            else
                []

        own @ (nodeChildren node |> List.collect walk)

    let findings = walk ctx.Tree
    addViolations findings ctx

/// Skip intentional test literals unless the host explicitly includes test-file findings.
///
/// decision: test assertions routinely pass expected true/false values positionally, so this
/// detector shares the magic detectors' test-file exemption and opt-in policy.
let analyzeOpaqueBooleanLiteral (ctx: AnalysisContext) : AnalysisContext =
    if
        not ctx.Options.OpaqueBoolean.Enabled
        || (not ctx.Options.OpaqueBoolean.IncludeTestFiles && isTestFile ctx.FileName)
    then
        ctx
    else
        analyzeEnabledOpaqueBooleans ctx

/// Register opaque boolean detection in the shared pipeline.
let detector: Detector =
    {
        Name = "opaqueBoolean"
        Run = analyzeOpaqueBooleanLiteral
    }
