module Energy.Core.Detectors.MagicString

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context
open Energy.Core.Detectors.TestFile

type MagicStringOptions =
    { Enabled: bool
      MinDuplicates: int
      Allowlist: string list
      // decision: test files are exempt by default because tests intentionally compare against
      // literal values (using a constant would hide a wrong constant); the flag lets a user opt
      // back in (e.g. to audit fixtures that live under a test/ directory).
      IncludeTestFiles: bool }

let defaultOptions =
    { Enabled = true
      MinDuplicates = 2
      Allowlist = [ ""; "utf-8"; "__main__" ]
      IncludeTestFiles = false }

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

let analyzeMagicStrings
    (tree: Node)
    (positions: PositionLookup)
    (language: LanguageAdapter)
    (fileName: string)
    (options: MagicStringOptions)
    : EnergyViolation list =
    if not options.Enabled || (not options.IncludeTestFiles && isTestFile fileName) then
        []
    else
        let isLiteral node =
            language.NodeTypes.StringLiteral |> Option.exists ((=) (nodeType node))

        let rec traverse (node: Node) : (Node * string) list =
            let ownCandidate =
                if isLiteral node then
                    let content = stripQuotes (nodeText node)

                    let isDecisionPoint =
                        isEqualityComparisonOperand language node
                        || isMembershipOperand language node content
                        || isKeyOrIndexPosition language node

                    let isExempt =
                        isDocstring language node
                        || language.IsFormattedOrInterpolatedString node
                        || List.contains content options.Allowlist
                        || content.Length <= 1

                    if isDecisionPoint && not isExempt then
                        [ node, content ]
                    else
                        []
                else
                    []

            ownCandidate @ (nodeChildren node |> List.collect traverse)

        traverse tree
        |> List.groupBy snd
        |> List.choose (fun (content, group) ->
            if group.Length < options.MinDuplicates then
                None
            else
                let first, _ = List.head group
                let position = positions.toPosition (nodeStartIndex first)

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

let detector: Detector =
    { Name = "magicString"
      Run = fun ctx -> analyzeMagicStrings ctx.Tree ctx.Positions ctx.Language ctx.FileName defaultOptions }
