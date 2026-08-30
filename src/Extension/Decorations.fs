module Energy.Extension.Decorations

open Fable.Core
open Fable.Core.JsInterop

open Energy.Core.Violation
open Energy.Extension.ConfigurationValues
open Energy.Extension.DecorationModel
open Energy.Extension.VscodeDocument
open Energy.Extension.VscodeHost
open Energy.Extension.VscodePresentation

type DecorationSet =
    { HighEnergy: obj
      MediumEnergy: obj
      LowEnergy: obj
      ComplexityHeat: obj array }

let private heatBandAlphas = [| 0.1; 0.18; 0.28; 0.42 |]

[<Import("Buffer", "node:buffer")>]
let private buffer: obj = nativeOnly

[<Emit("$0.from($1).toString('base64')")>]
let private asBase64 (bufferConstructor: obj) (value: string) : string = nativeOnly

let private lightningIcon color =
    let svg =
        sprintf
            """<svg width="16" height="16" xmlns="http://www.w3.org/2000/svg"><circle cx="8" cy="8" r="7" fill="%s" opacity="0.95"/><path d="M6 3 L10 8 L8.5 8 L10.5 13 L6.5 8 L8 8 Z" fill="white" stroke="white" stroke-width="0.3"/></svg>"""
            color

    uriFromString ("data:image/svg+xml;base64," + asBase64 buffer svg)

let private decorationOptions backgroundColor gutterIcon =
    createObj
        [ "backgroundColor" ==> backgroundColor
          "borderRadius" ==> "2px"
          "gutterIconPath" ==> gutterIcon
          "gutterIconSize" ==> "contain" ]

let createDecorations (colors: EnergyColors) : DecorationSet =
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
                    [ "backgroundColor" ==> hexToRgba colors.HighEnergy alpha defaultEnergyColors.HighEnergy ]))

    { HighEnergy = create colors.HighEnergy defaultEnergyColors.HighEnergy
      MediumEnergy = create colors.MediumEnergy defaultEnergyColors.MediumEnergy
      LowEnergy = create colors.LowEnergy defaultEnergyColors.LowEnergy
      ComplexityHeat = heat }

let disposeDecorations (decorations: DecorationSet) =
    [ decorations.HighEnergy; decorations.MediumEnergy; decorations.LowEnergy ]
    |> List.iter dispose

    decorations.ComplexityHeat |> Array.iter dispose

let private makeDecorationOption violation lineText =
    let range = rangeFor lineText violation

    createObj
        [ "range" ==> makeRange range.StartLine range.StartColumn range.EndLine range.EndColumn
          "hoverMessage" ==> "🔋 Energy Violation: " + violation.Message ]

let applyDecorations (editor: obj) (decorations: DecorationSet) (violations: EnergyViolation list) =
    let document = editorDocument editor

    let rangesFor severity =
        violations
        |> List.filter (fun violation -> violation.Severity = severity)
        |> List.map (fun violation -> makeDecorationOption violation (documentLineText document violation.Line))
        |> List.toArray

    setDecorations editor decorations.HighEnergy (rangesFor High)
    setDecorations editor decorations.MediumEnergy (rangesFor Medium)
    setDecorations editor decorations.LowEnergy (rangesFor Low)

    heatRanges
        (documentLineCount document)
        (documentLineText document)
        decorations.ComplexityHeat.Length
        violations
    |> Array.iteri (fun index ranges ->
        ranges
        |> List.map (fun range ->
            makeRange range.StartLine range.StartColumn range.EndLine range.EndColumn)
        |> List.toArray
        |> setDecorations editor decorations.ComplexityHeat.[index])
