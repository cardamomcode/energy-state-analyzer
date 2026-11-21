// # MagicString
//
// Decision-string analysis counts exact string content used in comparisons, membership, and indexing.
// Standalone, formatted, short, allowlisted, and excluded test strings do not contribute. Eligible
// occurrences are grouped across the file; a group meeting the configured count emits one low finding at
// its first occurrence.
//
// Reading path: [Source guide](../../../docs/source-guide.md).

module Energy.Core.Detectors.MagicString


open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Detectors.TestFile

/// Remove the surrounding quote characters from a string literal's text.
///
/// decision: magic-string options live in Core.Config as the single source of truth; this detector
/// reads them from ctx.Options so it no longer re-exports a module-level copy.

let private stripQuotes (text: string) =
    if text.Length >= 2 then
        text.Substring(1, text.Length - 2)
    else
        text

/// Treat a literal directly under an expression statement as standalone string content, excluding it from
/// decision-string candidates.
let private isDocstring (language: LanguageAdapter) (node: Node) =
    nodeParent node
    |> Option.exists (fun parent -> language.NodeTypes.ExpressionStatement |> Option.exists ((=) (nodeType parent)))

/// Check whether a literal is either operand of an adapter-normalized equality at its parent or
/// grandparent, accommodating wrapper nodes.
let private isEqualityComparisonOperand (language: LanguageAdapter) (node: Node) =
    // decision: checks both parent and grandparent because F# wraps literal operands in `const`
    // before their infix expression; node ids identify one live tree node across JS wrappers.
    [ nodeParent node; nodeParent node |> Option.bind nodeParent ]
    |> List.choose id
    |> List.exists (fun candidate ->
        language.GetEqualityComparisons candidate
        |> List.exists (fun comparison ->
            nodeId comparison.Left = nodeId node || nodeId comparison.Right = nodeId node))

/// Check whether the literal's text occurs in a membership comparison exposed at its grandparent.
let private isMembershipOperand (language: LanguageAdapter) (node: Node) (content: string) =
    nodeParent node
    |> Option.bind nodeParent
    |> Option.exists (fun container ->
        language.GetMembershipComparisons container
        |> List.exists (fun comparison -> List.contains content comparison.Values))

/// Recognize a literal directly inside an adapter-declared subscript node.
let private isKeyOrIndexPosition (language: LanguageAdapter) (node: Node) =
    nodeParent node
    |> Option.exists (fun parent -> List.contains (nodeType parent) language.SubscriptNodeTypes)

/// Select a string used in equality, membership, or indexing, excluding standalone, formatted, allowlisted,
/// and one-character values. Return at most one node/content pair.
let private decisionStringCandidate (ctx: AnalysisContext) (node: Node) : (Node * string) list =
    let isLiteral =
        ctx.Language.NodeTypes.StringLiteral |> Option.exists ((=) (nodeType node))

    if not isLiteral then
        []
    else
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

/// Collect eligible decision strings outside excluded Rust test modules, group by exact content, and report
/// groups meeting MinDuplicates. Each low finding anchors at the group's first occurrence and reports its
/// count.
let private analyzeEnabledMagicStrings (ctx: AnalysisContext) : AnalysisContext =
    /// Collect this node's eligible decision string and descendant candidates, pruning excluded Rust
    /// test modules.
    let rec traverse (node: Node) : (Node * string) list =
        if
            not ctx.Options.MagicString.IncludeTestFiles
            && isRustTestModule ctx.Language node
        then
            []
        else
            let ownCandidate = decisionStringCandidate ctx node
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
                    {
                        Line = position.Line
                        Column = position.Column
                        Type = Magic
                        Severity = Low
                        Message =
                            sprintf
                                "Magic string: \"%s\" is compared/keyed against directly %d time(s). Consider extracting to a named constant or enum."
                                content
                                group.Length
                        Hotspots = []
                    })

    addViolations findings ctx

/// Apply magic-string detection only when enabled and allowed for this filename; skipped files preserve the
/// context unchanged.
let analyzeMagicStrings (ctx: AnalysisContext) : AnalysisContext =
    if
        not ctx.Options.MagicString.Enabled
        || (not ctx.Options.MagicString.IncludeTestFiles && isTestFile ctx.FileName)
    then
        ctx
    else
        analyzeEnabledMagicStrings ctx

/// Register this detector's stable internal name and context transformation for the shared pipeline;
/// Analyze controls stage ordering.
let detector: Detector =
    {
        Name = "magicString"
        Run = analyzeMagicStrings
    }
