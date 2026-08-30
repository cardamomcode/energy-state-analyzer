module Energy.Core.Detectors.MagicNumber

open System

open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Context

type MagicNumberOptions =
    { Enabled: bool; Allowlist: float list }

let defaultOptions =
    { Enabled = true
      Allowlist = [ 0.0; 1.0; -1.0; 2.0 ] }

// The "Magic Number" detector exempts the small set of structural idioms where a literal is
// self-explanatory, while keeping significant values outside named bindings visible.
//
// decision: recognizes test-file names by path segment and camel-case word boundaries rather than
// a substring search — names such as latest_pricing.py must still be analyzed.
let private splitIntoWords (text: string) : string list =
    let _, separated =
        text
        |> Seq.fold
            (fun (previous, result) current ->
                let isCamelCaseBoundary =
                    previous
                    |> Option.exists (fun previous ->
                        (Char.IsLower previous || Char.IsDigit previous) && Char.IsUpper current)

                let separator = if isCamelCaseBoundary then " " else ""
                Some current, result + separator + string current)
            (None, "")

    separated.Split([| ' '; '_'; '-'; '.' |])
    |> Array.filter (String.IsNullOrWhiteSpace >> not)
    |> Array.toList

let private isTestFile (fileName: string) : bool =
    let segments =
        fileName.Replace("\\", "/").Split('/')
        |> Array.filter (String.IsNullOrWhiteSpace >> not)

    let isTestDirectory (segment: string) =
        let normalized = segment.ToLowerInvariant()
        normalized = "test" || normalized = "tests"

    if segments |> Array.exists isTestDirectory then
        true
    else
        let baseName = segments |> Array.tryLast |> Option.defaultValue ""
        let extensionStart = baseName.LastIndexOf('.')

        let stem =
            if extensionStart > 0 then
                baseName.Substring(0, extensionStart)
            else
                baseName

        match splitIntoWords stem |> List.map _.ToLowerInvariant() with
        | first :: _ when first = "test" -> true
        | words -> words |> List.tryLast = Some "test"

let private isNodeType (expected: NodeType option) (node: Node) : bool =
    expected |> Option.exists ((=) (nodeType node))

let private hasEnclosingFunction (language: LanguageAdapter) (assignmentNode: Node) : bool =
    let rec loop current =
        match nodeParent current with
        | Some parent when language.IsFunctionDefinition parent -> true
        | Some parent -> loop parent
        | None -> false

    loop assignmentNode

let private isInConstantContext (language: LanguageAdapter) (node: Node) : bool =
    let rec loop current =
        match nodeParent current with
        | None -> false
        | Some parent when language.IsExplicitConstant parent -> true
        | Some parent when isNodeType language.NodeTypes.Assignment parent ->
            // decision: an F# function definition shares its assignment-shaped node with a value
            // binding, so a literal in its body is computed logic rather than a named constant.
            if language.IsFunctionDefinition parent then
                false
            else
                let grandparent =
                    match nodeParent parent with
                    | Some expression when isNodeType language.NodeTypes.ExpressionStatement expression ->
                        nodeParent expression
                    | other -> other

                match grandparent with
                | Some moduleNode when isNodeType language.NodeTypes.Module moduleNode ->
                    not (hasEnclosingFunction language parent)
                | Some exportNode when isNodeType language.NodeTypes.ExportStatement exportNode ->
                    nodeParent exportNode |> Option.exists (isNodeType language.NodeTypes.Module)
                | _ -> loop parent
        | Some parent -> loop parent

    loop node

let private signedValue (node: Node) (rawValue: float) : float =
    match nodeParent node with
    | Some parent ->
        match nodeChildren parent with
        | sign :: _ :: [] when nodeText sign = "-" -> -rawValue
        | _ -> rawValue
    | None -> rawValue

let analyzeMagicNumbers
    (tree: Node)
    (positions: PositionLookup)
    (language: LanguageAdapter)
    (fileName: string)
    (options: MagicNumberOptions)
    : EnergyViolation list =
    if not options.Enabled || isTestFile fileName then
        []
    else
        let isLiteral node =
            isNodeType language.NodeTypes.IntegerLiteral node
            || isNodeType language.NodeTypes.FloatLiteral node

        let rec traverse (node: Node) : EnergyViolation list =
            if isLiteral node then
                // decision: parses every numeric literal as float because TypeScript uses one node
                // type for integers and floats; parsing as int would turn 1.08 into allowlisted 1.
                match Double.TryParse(nodeText node) with
                | true, rawValue ->
                    let value = signedValue node rawValue

                    let isExempt =
                        List.contains value options.Allowlist
                        || isInConstantContext language node
                        || (nodeParent node
                            |> Option.exists (fun parent -> List.contains (nodeType parent) language.SubscriptNodeTypes))
                        || language.IsDefaultParameterValue node

                    if isExempt then
                        []
                    else
                        let position = positions.toPosition (nodeStartIndex node)

                        [ { Line = position.Line
                            Column = position.Column
                            Type = Magic
                            Severity = Low
                            Message =
                              sprintf "Magic number: %s. Consider extracting to a named constant." (nodeText node)
                            Hotspots = [] } ]
                | false, _ -> []
            // A literal's children are fragments of the same value in some grammars, not separate
            // values; never descend after evaluating one.
            else
                nodeChildren node |> List.collect traverse

        traverse tree

let detector: Detector =
    { Name = "magicNumber"
      Run = fun ctx -> analyzeMagicNumbers ctx.Tree ctx.Positions ctx.Language ctx.FileName defaultOptions }
