module Energy.Extension.DecorationModel

open System

open Energy.Core.Violation

// Pure editor-decoration calculations. They intentionally know nothing about VS Code objects so
// Node-based Scriptorium tests can preserve presentation behavior without an extension host.

type RangeSpec =
    { StartLine: int
      StartColumn: int
      EndLine: int
      EndColumn: int }

let private elementHighlightWidth = 15

// decision: rejects malformed user colors and uses the documented default, so a workspace setting
// cannot make extension activation or decoration refresh fail.
let hexToRgba (value: string) (alpha: float) (fallback: string) =
    let digits =
        let normalized = value.Trim().TrimStart('#')

        if
            normalized.Length = 6
            && (normalized
                |> Seq.forall (fun character -> "0123456789abcdefABCDEF".Contains(string character)))
        then
            normalized
        else
            fallback.TrimStart('#')

    let channel index =
        Convert.ToInt32(digits.Substring(index * 2, 2), 16)

    sprintf "rgba(%d, %d, %d, %g)" (channel 0) (channel 1) (channel 2) alpha

let private firstNonWhitespace (text: string) =
    text |> Seq.tryFindIndex (Char.IsWhiteSpace >> not) |> Option.defaultValue -1

// decision: chooses highlight shape from violation category rather than expanding the shared
// violation model with UI-specific ranges; detector output stays host-independent for CLI use.
let rangeFor (lineText: string) (violation: EnergyViolation) =
    if violation.Type = Coherence then
        { StartLine = violation.Line
          StartColumn = 0
          EndLine = violation.Line
          EndColumn = lineText.Length }
    elif
        violation.Type = Nesting
        || violation.Type = Complexity
        || violation.Type = Cognitive
    then
        { StartLine = violation.Line
          StartColumn = firstNonWhitespace lineText
          EndLine = violation.Line
          EndColumn = lineText.Length }
    else
        { StartLine = violation.Line
          StartColumn = violation.Column
          EndLine = violation.Line
          EndColumn = min (violation.Column + elementHighlightWidth) lineText.Length }

// invariant: heat is normalized per violation, so each flagged function's worst contributing line
// gets the darkest band independently of other functions in the file.
let heatRanges (lineCount: int) (lineText: int -> string) (bandCount: int) (violations: EnergyViolation list) =
    let heatByLine =
        violations
        |> List.collect (fun violation ->
            match violation.Hotspots |> List.map _.Weight |> List.sortDescending with
            | maximum :: _ when maximum > 0 ->
                violation.Hotspots
                |> List.map (fun hotspot -> hotspot.Line, float hotspot.Weight / float maximum)
            | _ -> [])
        |> List.fold
            (fun current (line, intensity) ->
                let strongest =
                    current |> Map.tryFind line |> Option.defaultValue 0.0 |> max intensity

                Map.add line strongest current)
            Map.empty

    Array.init bandCount (fun _ -> [])
    |> Array.mapi (fun band _ ->
        heatByLine
        |> Map.toList
        |> List.choose (fun (line, intensity) ->
            let index = min (bandCount - 1) (int (floor (intensity * float bandCount)))

            if line >= 0 && line < lineCount && index = band then
                Some
                    { StartLine = line
                      StartColumn = 0
                      EndLine = line
                      EndColumn = (lineText line).Length }
            else
                None))
