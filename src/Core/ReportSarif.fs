module Energy.Core.ReportSarif

open Fable.Core.JsInterop

open Energy.Core.Report
open Energy.Core.Violation

let private sarifLevel =
    function
    | High -> "error"
    | Medium -> "warning"
    | Low -> "note"

// decision: reports one SARIF rule per emitted detector type and keeps the detector message on
// every result, so tools can group findings by stable rule ID while agents receive contextual fixes.
let renderSarif results =
    let findings =
        results
        |> List.collect (fun result -> result.Violations |> List.map (fun violation -> result.FilePath, violation))

    let rules =
        findings
        |> List.map snd
        |> List.map _.Type
        |> List.distinct
        |> List.sortBy violationTypeName
        |> List.map (fun violationType ->
            createObj
                [ "id" ==> violationRuleId violationType
                  "name" ==> violationTypeName violationType
                  "shortDescription"
                  ==> createObj
                          [ "text"
                            ==> ("Energy State Analyzer " + violationTypeName violationType + " finding") ]
                  "helpUri"
                  ==> "https://github.com/cardamomcode/energy-state-analyzer/tree/main/docs/detectors" ])
        |> List.toArray

    let sarifResults =
        findings
        |> List.map (fun (filePath, violation) ->
            let hotspots =
                violation.Hotspots
                |> List.map (fun hotspot -> createObj [ "line" ==> hotspot.Line + 1; "weight" ==> hotspot.Weight ])
                |> List.toArray

            createObj
                [ "ruleId" ==> violationRuleId violation.Type
                  "level" ==> sarifLevel violation.Severity
                  "message" ==> createObj [ "text" ==> violation.Message ]
                  "locations"
                  ==> [| createObj
                             [ "physicalLocation"
                               ==> createObj
                                       [ "artifactLocation" ==> createObj [ "uri" ==> filePath ]
                                         "region"
                                         ==> createObj
                                                 [ "startLine" ==> violation.Line + 1
                                                   "startColumn" ==> violation.Column + 1 ] ] ] |]
                  "properties"
                  ==> createObj
                          [ "energyStateSeverity" ==> severityName violation.Severity
                            "hotspots" ==> hotspots ] ])
        |> List.toArray

    createObj
        [ "$schema"
          ==> "https://docs.oasis-open.org/sarif/sarif/v2.1.0/errata01/os/schemas/sarif-schema-2.1.0.json"
          "version" ==> "2.1.0"
          "runs"
          ==> [| createObj
                     [ "tool"
                       ==> createObj
                               [ "driver"
                                 ==> createObj
                                         [ "name" ==> "Energy State Analyzer"
                                           "informationUri" ==> "https://github.com/cardamomcode/energy-state-analyzer"
                                           "rules" ==> rules ] ]
                       "results" ==> sarifResults ] |] ]
