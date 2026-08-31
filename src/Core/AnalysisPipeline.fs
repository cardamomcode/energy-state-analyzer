module Energy.Core.AnalysisPipeline

open Energy.Core.Context
open Energy.Core.Violation

type AnalysisFunc = AnalysisContext -> EnergyViolation list

type AnalysisHandler = AnalysisFunc -> AnalysisFunc

let empty: AnalysisFunc = fun _ -> []

let detector (analyze: AnalysisFunc) : AnalysisHandler = fun next ctx -> analyze ctx @ next ctx

let compose (handler1: AnalysisHandler) (handler2: AnalysisHandler) : AnalysisHandler =
    fun final ->
        let func = final |> handler2 |> handler1
        fun ctx -> func ctx
