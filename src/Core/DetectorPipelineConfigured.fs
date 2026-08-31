module Energy.Core.DetectorPipelineConfigured

open Energy.Core.Context
open Energy.Core.AnalysisPipeline
open Energy.Core.DetectorPipeline

let pipeline (thresholds: AnalyzeThresholds) : AnalysisFunc =
    compose (DetectorPipelineComplexity.handler thresholds) (DetectorPipelinePatterns.handler thresholds) empty

let runWith (thresholds: AnalyzeThresholds) (ctx: AnalysisContext) = pipeline thresholds ctx
