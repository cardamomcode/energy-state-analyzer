module Energy.Tests.CognitiveRuleCases

open Energy.Languages

/// Exact scores for original delivery-policy examples, including clean and branching cases.
let cases =
    [
        Python.pythonLanguageAdapter,
        "python/cognitive_rules.py",
        [
            "straightLine", 0
            "booleanRun", 1
            "mixedRuns", 3
            "groupedRun", 1
            "negatedGroup", 2
            "plainElse", 2
            "flatChain", 3
            "nestedChain", 4
            "nestedChecks", 3
            "dispatch", 3
            "loopBody", 3
            "lambdaBody", 2
            "emptyLambda", 0
            "guardReturn", 1
            "catches", 4
            "cleanup", 0
            "nestedFunction", 2
            "emptyFunction", 0
            "bracedElseCheck", 4
        ]
        TypeScript.typeScriptLanguageAdapter,
        "typescript/cognitiveRules.ts",
        [
            "straightLine", 0
            "booleanRun", 1
            "mixedRuns", 3
            "groupedRun", 1
            "negatedGroup", 2
            "plainElse", 2
            "flatChain", 3
            "nestedChain", 4
            "nestedChecks", 3
            "dispatch", 3
            "loopBody", 3
            "lambdaBody", 2
            "emptyLambda", 0
            "guardReturn", 1
            "unbracedChecks", 3
            "doLoop", 3
            "nestedFunction", 2
            "emptyFunction", 0
            "cleanup", 0
            "catches", 3
            "labelJump", 2
            "ordinaryBreak", 1
            "bracedElseCheck", 4
            "multilineRuns", 2
            "conditionalCondition", 2
            "booleanArgument", 1
            "booleanAssignment", 1
        ]
        FSharp.fSharpLanguageAdapter,
        "fsharp/CognitiveRules.fs",
        [
            "straightLine", 0
            "booleanRun", 1
            "mixedRuns", 3
            "groupedRun", 1
            "negatedGroup", 2
            "plainElse", 2
            "flatChain", 3
            "nestedChain", 4
            "nestedChecks", 3
            "dispatch", 3
            "loopBody", 3
            "lambdaBody", 2
            "emptyLambda", 0
            "unitConditional", 1
            "catches", 4
            "cleanup", 0
            "nestedFunction", 2
            "emptyFunction", 0
            "bracedElseCheck", 4
        ]
        Kotlin.kotlinLanguageAdapter,
        "kotlin/CognitiveRules.kt",
        [
            "straightLine", 0
            "booleanRun", 1
            "mixedRuns", 3
            "groupedRun", 1
            "negatedGroup", 2
            "plainElse", 2
            "flatChain", 3
            "nestedChain", 4
            "nestedChecks", 3
            "dispatch", 3
            "loopBody", 3
            "lambdaBody", 2
            "emptyLambda", 0
            "guardReturn", 1
            "unbracedChecks", 3
            "doLoop", 3
            "nestedFunction", 2
            "emptyFunction", 0
            "cleanup", 0
            "catches", 4
            "labelJump", 2
            "labelContinue", 2
            "labelReturn", 1
            "ordinaryBreak", 1
            "bracedElseCheck", 4
        ]
        CPlusPlus.cPlusPlusLanguageAdapter,
        "cpp/cognitive_rules.cpp",
        [
            "straightLine", 0
            "booleanRun", 1
            "mixedRuns", 3
            "groupedRun", 1
            "negatedGroup", 2
            "plainElse", 2
            "flatChain", 3
            "nestedChain", 4
            "nestedChecks", 3
            "dispatch", 3
            "loopBody", 3
            "lambdaBody", 2
            "emptyLambda", 0
            "guardReturn", 1
            "unbracedChecks", 3
            "doLoop", 3
            "catches", 4
            "labelJump", 1
            "ordinaryBreak", 1
            "bracedElseCheck", 4
        ]
        CSharp.cSharpLanguageAdapter,
        "csharp/CognitiveRules.cs",
        [
            "straightLine", 0
            "booleanRun", 1
            "mixedRuns", 3
            "groupedRun", 1
            "negatedGroup", 2
            "plainElse", 2
            "flatChain", 3
            "nestedChain", 4
            "nestedChecks", 3
            "dispatch", 3
            "loopBody", 3
            "lambdaBody", 2
            "emptyLambda", 0
            "guardReturn", 1
            "unbracedChecks", 3
            "doLoop", 3
            "nestedFunction", 2
            "emptyFunction", 0
            "cleanup", 0
            "catches", 4
            "labelJump", 1
            "ordinaryBreak", 1
            "bracedElseCheck", 4
        ]
    ]
