module Energy.Tests.DetectorFixtureMatrixTests

open Scriptorium.Quill
open type Scriptorium.Quill.Test

open Energy.Core.Violation
open Energy.Languages
open Energy.Tests.TestUtils

let private fixture languageLabel language fixture expectations =
    { LanguageLabel = languageLabel
      Language = language
      Fixture = fixture
      Expectations = expectations }

let private cSharpFunctionName (name: FunctionName) =
    let (FunctionName text) = name
    FunctionName(System.Char.ToUpperInvariant(text.[0]).ToString() + text.Substring(1))

let private expectationsFor
    (language: Energy.Core.LanguageAdapter.LanguageAdapter)
    (expectations: FixtureExpectation list)
    =
    if language.Id = "csharp" then
        expectations
        |> List.map (function
            | StaysClean name -> StaysClean(cSharpFunctionName name)
            | ProducesFinding(name, severity) -> ProducesFinding(cSharpFunctionName name, severity))
    else
        expectations

let private allLanguages python typeScript fsharp kotlin cPlusPlus cSharp =
    [ fixture "Python" Python.pythonLanguageAdapter python []
      fixture "TypeScript" TypeScript.typeScriptLanguageAdapter typeScript []
      fixture "F#" FSharp.fSharpLanguageAdapter fsharp []
      fixture "Kotlin" Kotlin.kotlinLanguageAdapter kotlin []
      fixture "C++" CPlusPlus.cPlusPlusLanguageAdapter cPlusPlus []
      fixture "C#" CSharp.cSharpLanguageAdapter cSharp [] ]

type private FixturePaths =
    { Python: string
      TypeScript: string
      FSharp: string
      Kotlin: string
      CPlusPlus: string
      CSharp: string }

let private commonCases (paths: FixturePaths) expectations =
    allLanguages
        ("python/" + paths.Python)
        ("typescript/" + paths.TypeScript)
        ("fsharp/" + paths.FSharp)
        ("kotlin/" + paths.Kotlin)
        ("cpp/" + paths.CPlusPlus)
        ("csharp/" + paths.CSharp)
    |> List.map (fun item ->
        { item with
            Expectations = expectationsFor item.Language expectations })

// decision: keeps the cross-language detector contract in one compact, source-oriented catalogue.
// A reviewer can read each named function in the fixture beside its positive or negative outcome.
// invariant: a detector added to the product has either a parity row here or an explicitly
// language-specific regression test explaining why a shared example is not meaningful.
let tests =
    let magicNumbers =
        commonCases
            { Python = "magic_number.py"
              TypeScript = "magicNumber.ts"
              FSharp = "MagicNumber.fs"
              Kotlin = "MagicNumber.kt"
              CPlusPlus = "magic_number.cpp"
              CSharp = "MagicNumber.cs" }
            [ StaysClean(FunctionName "cleanCommonValues")
              StaysClean(FunctionName "cleanNegativeValue")
              ProducesFinding(FunctionName "flaggedMagicNumbers", None) ]

    let magicStrings =
        commonCases
            { Python = "magic_string.py"
              TypeScript = "magicString.ts"
              FSharp = "MagicString.fs"
              Kotlin = "MagicString.kt"
              CPlusPlus = "magic_string.cpp"
              CSharp = "MagicString.cs" }
            [ StaysClean(FunctionName "cleanValues")
              ProducesFinding(FunctionName "flaggedMagicString", None) ]

    let nesting =
        commonCases
            { Python = "nesting.py"
              TypeScript = "nesting.ts"
              FSharp = "Nesting.fs"
              Kotlin = "Nesting.kt"
              CPlusPlus = "nesting.cpp"
              CSharp = "Nesting.cs" }
            [ StaysClean(FunctionName "cleanShallowNesting")
              ProducesFinding(FunctionName "flaggedDeepNesting", Some Medium)
              ProducesFinding(FunctionName "flaggedSevereNesting", Some High)
              ProducesFinding(FunctionName "flaggedTryNesting", None) ]

    let cyclomatic =
        commonCases
            { Python = "cyclomatic_complexity.py"
              TypeScript = "cyclomaticComplexity.ts"
              FSharp = "CyclomaticComplexity.fs"
              Kotlin = "CyclomaticComplexity.kt"
              CPlusPlus = "cyclomatic_complexity.cpp"
              CSharp = "CyclomaticComplexity.cs" }
            [ StaysClean(FunctionName "cleanSimpleFunction")
              ProducesFinding(FunctionName "flaggedComplexFunction", Some Medium)
              ProducesFinding(FunctionName "flaggedSevereFunction", Some High) ]

    let cognitive =
        commonCases
            { Python = "cognitive_complexity.py"
              TypeScript = "cognitiveComplexity.ts"
              FSharp = "CognitiveComplexity.fs"
              Kotlin = "CognitiveComplexity.kt"
              CPlusPlus = "cognitive_complexity.cpp"
              CSharp = "CognitiveComplexity.cs" }
            [ StaysClean(FunctionName "cleanSimpleFunction")
              ProducesFinding(FunctionName "flaggedComplexFunction", Some Medium)
              ProducesFinding(FunctionName "flaggedSevereFunction", Some High) ]

    let parameters =
        commonCases
            { Python = "parameter_count.py"
              TypeScript = "parameterCount.ts"
              FSharp = "ParameterCount.fs"
              Kotlin = "ParameterCount.kt"
              CPlusPlus = "parameter_count.cpp"
              CSharp = "ParameterCount.cs" }
            [ StaysClean(FunctionName "cleanFewParams")
              ProducesFinding(FunctionName "flaggedManyParams", Some Medium)
              ProducesFinding(FunctionName "flaggedTooManyParams", Some High) ]

    let primitiveObsession =
        commonCases
            { Python = "primitive_obsession.py"
              TypeScript = "primitiveObsession.ts"
              FSharp = "PrimitiveObsession.fs"
              Kotlin = "PrimitiveObsession.kt"
              CPlusPlus = "primitive_obsession.cpp"
              CSharp = "PrimitiveObsession.cs" }
            [ StaysClean(FunctionName "cleanDistinctTypes")
              ProducesFinding(FunctionName "flaggedSwapRisk", None)
              ProducesFinding(FunctionName "flaggedStringlyTyped", None) ]

    let opaqueBoolean =
        allLanguages
            "python/opaque_boolean.py"
            "typescript/opaqueBoolean.ts"
            "fsharp/OpaqueBoolean.fs"
            "kotlin/OpaqueBoolean.kt"
            "cpp/opaque_boolean.cpp"
            "csharp/OpaqueBoolean.cs"
        |> List.mapi (fun index item ->
            { item with
                Expectations =
                    [ ProducesFinding(FunctionName "flaggedPositionalBoolean", None)
                      ProducesFinding(FunctionName "flaggedPositionalBooleanAmongOthers", None)
                      StaysClean(
                          FunctionName(
                              [ "suppressedKeywordArgument"
                                "suppressedObjectLiteralField"
                                "suppressedNamedArgument"
                                "suppressedNamedArgument"
                                "suppressedLabeledAggregateField"
                                "suppressedNonCallUsage" ]
                              |> List.item index
                          )
                      )
                      StaysClean(FunctionName "suppressedNonCallUsage") ] })
        |> List.map (fun item ->
            { item with
                Expectations = expectationsFor item.Language item.Expectations })

    let logicalControlFlow =
        [ fixture
              "Python"
              Python.pythonLanguageAdapter
              "python/logical_control_flow.py"
              [ StaysClean(FunctionName "cleanExplicitIf")
                ProducesFinding(FunctionName "flaggedAndAsIf", Some Low)
                ProducesFinding(FunctionName "flaggedOrAsUnless", Some Low) ]
          fixture
              "TypeScript"
              TypeScript.typeScriptLanguageAdapter
              "typescript/logicalControlFlow.ts"
              [ StaysClean(FunctionName "cleanExplicitIf")
                ProducesFinding(FunctionName "flaggedAndAsIf", Some Low)
                ProducesFinding(FunctionName "flaggedOrAsUnless", Some Low) ]
          fixture
              "C++"
              CPlusPlus.cPlusPlusLanguageAdapter
              "cpp/logical_control_flow.cpp"
              [ StaysClean(FunctionName "cleanExplicitIf")
                ProducesFinding(FunctionName "flaggedAndAsIf", Some Low)
                ProducesFinding(FunctionName "flaggedOrAsUnless", Some Low) ]
          // C# rejects bare `condition && action()` and `condition || action()` expressions as
          // statements, so the source-level shorthand this detector recognizes is unavailable.
          fixture
              "C#"
              CSharp.cSharpLanguageAdapter
              "csharp/LogicalControlFlow.cs"
              [ StaysClean(FunctionName "CleanExplicitIf") ] ]

    let matchOpportunity =
        commonCases
            { Python = "match_opportunity.py"
              TypeScript = "matchOpportunity.ts"
              FSharp = "MatchOpportunity.fs"
              Kotlin = "MatchOpportunity.kt"
              CPlusPlus = "match_opportunity.cpp"
              CSharp = "MatchOpportunity.cs" }
            [ StaysClean(FunctionName "cleanMixedConditions")
              ProducesFinding(FunctionName "flaggedThreeWayChain", None) ]

    // F# has no equivalent block-shaped conditional in its grammar, so its fixture makes the
    // supported-language boundary visible as a clean case rather than pretending it is parity.
    let inversion =
        [ fixture
              "Python"
              Python.pythonLanguageAdapter
              "python/inversion.py"
              [ StaysClean(FunctionName "cleanEarlyReturn")
                ProducesFinding(FunctionName "flaggedDominantIf", None)
                ProducesFinding(FunctionName "flaggedValidationChain", None) ]
          fixture
              "TypeScript"
              TypeScript.typeScriptLanguageAdapter
              "typescript/inversion.ts"
              [ StaysClean(FunctionName "cleanEarlyReturn")
                ProducesFinding(FunctionName "flaggedDominantIf", None)
                ProducesFinding(FunctionName "flaggedValidationChain", None) ]
          fixture
              "Kotlin"
              Kotlin.kotlinLanguageAdapter
              "kotlin/Inversion.kt"
              [ StaysClean(FunctionName "cleanEarlyReturn")
                ProducesFinding(FunctionName "flaggedDominantIf", None)
                ProducesFinding(FunctionName "flaggedValidationChain", None) ]
          fixture
              "C++"
              CPlusPlus.cPlusPlusLanguageAdapter
              "cpp/inversion.cpp"
              [ StaysClean(FunctionName "cleanEarlyReturn")
                ProducesFinding(FunctionName "flaggedDominantIf", None)
                ProducesFinding(FunctionName "flaggedValidationChain", None) ]
          fixture
              "F# (grammar limitation)"
              FSharp.fSharpLanguageAdapter
              "fsharp/Inversion.fs"
              [ StaysClean(FunctionName "unflaggedValidationChain") ]
          fixture
              "C#"
              CSharp.cSharpLanguageAdapter
              "csharp/Inversion.cs"
              [ StaysClean(FunctionName "CleanEarlyReturn")
                ProducesFinding(FunctionName "FlaggedDominantIf", None)
                ProducesFinding(FunctionName "FlaggedValidationChain", None) ] ]

    let errorShadowing =
        allLanguages
            "python/error_shadowing.py"
            "typescript/errorShadowing.ts"
            "fsharp/ErrorShadowing.fs"
            "kotlin/error_shadowing.kt"
            "cpp/error_shadowing.cpp"
            "csharp/ErrorShadowing.cs"
        |> List.map (fun item ->
            { item with
                Expectations =
                    [ StaysClean(FunctionName "cleanPath")
                      StaysClean(FunctionName "shadowedByError") ]
                    |> expectationsFor item.Language })
        |> List.append
            [ fixture
                  "Python (recovery-dominated regression)"
                  Python.pythonLanguageAdapter
                  "python/error_shadowing_recovery_heavy.py"
                  [ ProducesFinding(FunctionName "recoveryDominates", None) ] ]

    testList (
        "Integration: detector fixture parity matrix",
        [ yield! detectorParityTests "magic numbers" Magic magicNumbers
          yield! detectorParityTests "magic strings" Magic magicStrings
          yield! detectorParityTests "nesting" Nesting nesting
          yield! detectorParityTests "cyclomatic complexity" Complexity cyclomatic
          yield! detectorParityTests "cognitive complexity" Cognitive cognitive
          yield! detectorParityTests "parameter count" Parameters parameters
          yield! detectorParityTests "primitive obsession" PrimitiveObsession primitiveObsession
          yield! detectorParityTests "opaque boolean" OpaqueBoolean opaqueBoolean
          yield! detectorParityTests "logical control flow" LogicalControlFlow logicalControlFlow
          yield! detectorParityTests "match opportunity" MatchOpportunity matchOpportunity
          yield! detectorParityTests "inversion" Inversion inversion
          yield! detectorParityTests "error shadowing" ErrorShadowing errorShadowing ]
    )
