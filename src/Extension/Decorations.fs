// # Decorations
//
// The decoration boundary allocates severity styles and progressively stronger complexity backgrounds,
// then replaces each style's ranges for an editor. DecorationModel.fs performs the pure calculations; this
// module owns host conversion and SVG gutter icons. The composition root disposes every handle on color
// replacement or deactivation.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Extension.Decorations

open Fable.Core
open Fable.Core.JsInterop

open Energy.Core.Violation
open Energy.Core.Config
open Energy.Extension.DecorationModel
open Energy.Extension.Vscode.Document
open Energy.Extension.Vscode.Host
open Energy.Extension.Vscode.Presentation

/// Activation-owned severity and heat-band handles; the owner disposes all handles when colors change or
/// the extension stops.
type DecorationSet =
    {
        HighEnergy: obj
        MediumEnergy: obj
        LowEnergy: obj
        ComplexityHeat: obj array
    }

/// Increasing background opacity for the four complexity heat bands.
let private heatBandAlphas = [| 0.1; 0.18; 0.28; 0.42 |]

/// Node Buffer constructor used to encode inline SVG gutter icons.
[<Import("Buffer", "node:buffer")>]
let private buffer: obj = nativeOnly

/// Encode SVG text through Node Buffer for a data URI.
[<Emit("$0.from($1).toString('base64')")>]
let private asBase64 (bufferConstructor: obj) (value: string) : string = nativeOnly

/// Construct a colored lightning-bolt SVG and return its base64 data URI for the editor gutter.
let private lightningIcon color =
    let svg =
        sprintf
            """<svg width="16" height="16" xmlns="http://www.w3.org/2000/svg"><circle cx="8" cy="8" r="7" fill="%s" opacity="0.95"/><path d="M6 3 L10 8 L8.5 8 L10.5 13 L6.5 8 L8 8 Z" fill="white" stroke="white" stroke-width="0.3"/></svg>"""
            color

    uriFromString ("data:image/svg+xml;base64," + asBase64 buffer svg)

/// Build the VS Code background and gutter-icon options for one severity style.
let private decorationOptions backgroundColor gutterIcon =
    createObj
        [
            "backgroundColor" ==> backgroundColor
            "borderRadius" ==> "2px"
            "gutterIconPath" ==> gutterIcon
            "gutterIconSize" ==> "contain"
        ]

/// Allocate three severity styles and four heat styles from configured colors and opacity. The caller owns
/// their disposal; creating handles does not apply them to an editor.
let createDecorations (colors: EnergyColors) : DecorationSet =
    /// Allocate one severity style using the configured color and its fallback background color.
    let create color fallback =
        createTextEditorDecorationType
            window
            (decorationOptions (hexToRgba color colors.BackgroundOpacity fallback) (lightningIcon color))

    let heat =
        heatBandAlphas
        |> Array.map (fun alpha ->
            createTextEditorDecorationType
                window
                (createObj
                    [
                        "backgroundColor"
                        ==> hexToRgba colors.HighEnergy alpha defaultEnergyColors.HighEnergy
                    ]))

    {
        HighEnergy = create colors.HighEnergy defaultEnergyColors.HighEnergy
        MediumEnergy = create colors.MediumEnergy defaultEnergyColors.MediumEnergy
        LowEnergy = create colors.LowEnergy defaultEnergyColors.LowEnergy
        ComplexityHeat = heat
    }

/// Dispose every severity and heat-band handle owned by a decoration set.
let disposeDecorations (decorations: DecorationSet) =
    [ decorations.HighEnergy; decorations.MediumEnergy; decorations.LowEnergy ]
    |> List.iter dispose

    decorations.ComplexityHeat |> Array.iter dispose

/// Convert one finding into a trimmed source range and hover message using the document line text.
let private makeDecorationOption violation lineText =
    let range = rangeFor lineText violation

    createObj
        [
            "range"
            ==> makeRange
                    (Line range.StartLine)
                    (Column range.StartColumn)
                    (Line range.EndLine)
                    (Column range.EndColumn)
            "hoverMessage" ==> "🔋 Energy Violation: " + violation.Message
        ]

/// Replace every severity and heat-band decoration on the supplied editor. Empty finding groups clear prior
/// ranges; line text and document length feed the pure DecorationModel calculations.
let applyDecorations (editor: obj) (decorations: DecorationSet) (violations: EnergyViolation list) =
    let document = editorDocument editor

    /// Build this severity's decoration options using the current document line text.
    let rangesFor severity =
        violations
        |> List.filter (fun violation -> violation.Severity = severity)
        |> List.map (fun violation -> makeDecorationOption violation (documentLineText document violation.Line))
        |> List.toArray

    setDecorations editor decorations.HighEnergy (rangesFor High)
    setDecorations editor decorations.MediumEnergy (rangesFor Medium)
    setDecorations editor decorations.LowEnergy (rangesFor Low)

    heatRanges
        (LineCount(documentLineCount document))
        (documentLineText document)
        (BandCount decorations.ComplexityHeat.Length)
        violations
    |> Array.iteri (fun index ranges ->
        ranges
        |> List.map (fun range ->
            makeRange (Line range.StartLine) (Column range.StartColumn) (Line range.EndLine) (Column range.EndColumn))
        |> List.toArray
        |> setDecorations editor decorations.ComplexityHeat.[index])
