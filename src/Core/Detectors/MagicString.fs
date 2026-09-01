module Energy.Core.Detectors.MagicString


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Detectors.TestFile

type MagicStringOptions = Energy.Core.Context.MagicStringOptions

let defaultOptions = defaultAnalyzeOptions.MagicString

let private stripQuotes (text: string) =
    if text.Length >= 2 then
        text.Substring(1, text.Length - 2)
    else
        text

let private isDocstring (language: LanguageAdapter) (node: Node) =
    nodeParent node
    |> Option.exists (fun parent -> language.NodeTypes.ExpressionStatement |> Option.exists ((=) (nodeType parent)))

let private isEqualityComparisonOperand (language: LanguageAdapter) (node: Node) =
    // decision: checks both parent and grandparent because F# wraps literal operands in `const`
    // before their infix expression; node ids identify one live tree node across JS wrappers.
    [ nodeParent node; nodeParent node |> Option.bind nodeParent ]
    |> List.choose id
    |> List.exists (fun candidate ->
        language.GetEqualityComparisons candidate
        |> List.exists (fun comparison ->
            nodeId comparison.Left = nodeId node || nodeId comparison.Right = nodeId node))

let private isMembershipOperand (language: LanguageAdapter) (node: Node) (content: string) =
    nodeParent node
    |> Option.bind nodeParent
    |> Option.exists (fun container ->
        language.GetMembershipComparisons container
        |> List.exists (fun comparison -> List.contains content comparison.Values))

let private isKeyOrIndexPosition (language: LanguageAdapter) (node: Node) =
    nodeParent node
    |> Option.exists (fun parent -> List.contains (nodeType parent) language.SubscriptNodeTypes)

let private analyzeEnabledMagicStrings (ctx: AnalysisContext) : AnalysisContext =
    let isLiteral node =
        ctx.Language.NodeTypes.StringLiteral |> Option.exists ((=) (nodeType node))

    let rec traverse (node: Node) : (Node * string) list =
        let ownCandidate =
            if isLiteral node then
                let content = stripQuotes (nodeText node)

                let isDecisionPoint =
                    isEqualityComparisonOperand ctx.Language node
                    || isMembershipOperand ctx.Language node content
                    || isKeyOrIndexPosition ctx.Language node

                let isExempt =
                    isDocstring ctx.Language node
                    || ctx.Language.IsFormattedOrInterpolatedString node
                    || List.contains content ctx.Options.MagicString.Allowlist
                    || content.Length <= 1

                if isDecisionPoint && not isExempt then
                    [ node, content ]
                else
                    []
            else
                []

        ownCandidate @ (nodeChildren node |> List.collect traverse)

    let findings =
        traverse ctx.Tree
        |> List.groupBy snd
        |> List.choose (fun (content, group) ->
            if group.Length < ctx.Options.MagicString.MinDuplicates then
                None
            else
                let first, _ = List.head group
                let position = ctx.Positions.toPosition (nodeStartIndex first)

                Some
                    { Line = position.Line
                      Column = position.Column
                      Type = Magic
                      Severity = Low
                      Message =
                        sprintf
                            "Magic string: \"%s\" is compared/keyed against directly %d time(s). Consider extracting to a named constant or enum."
                            content
                            group.Length
                      Hotspots = [] })

    addViolations findings ctx

let analyzeMagicStrings (ctx: AnalysisContext) : AnalysisContext =
    if
        not ctx.Options.MagicString.Enabled
        || (not ctx.Options.MagicString.IncludeTestFiles && isTestFile ctx.FileName)
    then
        ctx
    else
        analyzeEnabledMagicStrings ctx

let detector: Detector =
    { Name = "magicString"
      Run = analyzeMagicStrings }
