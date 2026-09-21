module Energy.Tests.AnonymousCallableTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Detectors.Cyclomatic
open Energy.Core.Detectors.Cognitive
open Energy.Languages
open Energy.Tests.CallableTestUtils
open Energy.Tests.TestUtils

/// Expected role, direct binding name, and explicit parameter count for one callable.
type private CallableExpectation = CallableRole * string option * int

/// Cross-language callable scenarios in their expected source traversal order.
let private cases =
    [
        "Python",
        Python.pythonLanguageAdapter,
        "python/anonymous_callables.py",
        [
            NamedDefinition, Some "named", 1
            BoundAnonymous ModuleBinding, Some "module_bound", 2
            BoundAnonymous ClassMemberBinding, Some "class_bound", 1
            NamedDefinition, Some "outer", 1
            InlineAnonymous, Some "local_bound", 1
            InlineAnonymous, None, 1
        ]
        "TypeScript",
        TypeScript.typeScriptLanguageAdapter,
        "typescript/anonymousCallables.ts",
        [
            NamedDefinition, Some "named", 1
            BoundAnonymous ModuleBinding, Some "moduleArrow", 2
            BoundAnonymous ModuleBinding, Some "moduleFunction", 1
            BoundAnonymous ClassMemberBinding, Some "classBound", 1
            NamedDefinition, Some "namedMethod", 1
            NamedDefinition, Some "outer", 1
            InlineAnonymous, Some "localBound", 1
            InlineAnonymous, None, 1
        ]
        "F#",
        FSharp.fSharpLanguageAdapter,
        "fsharp/AnonymousCallables.fs",
        [
            NamedDefinition, Some "named", 1
            NamedDefinition, Some "mutualFirst", 1
            NamedDefinition, Some "mutualSecond", 2
            BoundAnonymous ModuleBinding, Some "moduleBound", 2
            BoundAnonymous ClassMemberBinding, Some "ClassBound", 1
            NamedDefinition, Some "outer", 1
            InlineAnonymous, Some "localBound", 1
            InlineAnonymous, None, 1
        ]
        "Kotlin",
        Kotlin.kotlinLanguageAdapter,
        "kotlin/AnonymousCallables.kt",
        [
            NamedDefinition, Some "named", 1
            BoundAnonymous ModuleBinding, Some "moduleBound", 2
            BoundAnonymous ClassMemberBinding, Some "classBound", 1
            NamedDefinition, Some "namedMethod", 1
            NamedDefinition, Some "outer", 1
            InlineAnonymous, Some "localBound", 1
            InlineAnonymous, None, 0
        ]
        "C++",
        CPlusPlus.cPlusPlusLanguageAdapter,
        "cpp/anonymous_callables.cpp",
        [
            NamedDefinition, Some "named", 1
            BoundAnonymous ModuleBinding, Some "moduleBound", 2
            BoundAnonymous ClassMemberBinding, Some "classBound", 1
            NamedDefinition, Some "namedMethod", 1
            NamedDefinition, Some "outer", 0
            InlineAnonymous, Some "localBound", 1
            InlineAnonymous, None, 1
        ]
        "C#",
        CSharp.cSharpLanguageAdapter,
        "csharp/AnonymousCallables.cs",
        [
            BoundAnonymous ClassMemberBinding, Some "ClassBound", 2
            BoundAnonymous ClassMemberBinding, Some "ClassMethod", 1
            NamedDefinition, Some "NamedMethod", 1
            NamedDefinition, Some "Outer", 1
            InlineAnonymous, Some "localBound", 1
            InlineAnonymous, None, 1
        ]
    ]

/// Verify the shared callable contract against every bundled grammar.
let private classificationTests =
    cases
    |> List.map (fun (languageLabel, language, fixture, expected: CallableExpectation list) ->
        testAsync (
            sprintf "%s classifies named, bound, local, and inline callables" languageLabel,
            fun _ ->
                toAsync (
                    task {
                        let! source, tree = parseFixture language fixture
                        assertThat (nodeHasError tree) isFalse

                        let callables = collectCallableViews language tree
                        assertCallableSourceRanges source callables

                        let actual =
                            callables
                            |> List.map (fun callable ->
                                callable.Role, callable.BindingName, callable.Parameters.Length)

                        assertThat actual (isEqualTo expected)
                    }
                )
        ))
    |> fun tests -> testList ("Anonymous callable language adapters", tests)

/// Fixture, nested callable name, and exact standalone/enclosing cognitive scores.
let private boundaryCases =
    [
        "Python", Python.pythonLanguageAdapter, "python/anonymous_callable_analysis.py", "nestedCallableBoundary", 2, 1
        "TypeScript",
        TypeScript.typeScriptLanguageAdapter,
        "typescript/anonymousCallableAnalysis.ts",
        "nestedCallableBoundary",
        2,
        1
        "F#", FSharp.fSharpLanguageAdapter, "fsharp/AnonymousCallableAnalysis.fs", "nestedCallableBoundary", 3, 2
        "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/AnonymousCallableAnalysis.kt", "nestedCallableBoundary", 3, 2
        "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/anonymous_callable_analysis.cpp", "nestedCallableBoundary", 2, 1
        "C#", CSharp.cSharpLanguageAdapter, "csharp/AnonymousCallableAnalysis.cs", "NestedCallableBoundary", 2, 1
    ]

/// Sum cognitive hotspot weights for an exact-score consistency check.
let private hotspotTotal (violation: EnergyViolation) =
    violation.Hotspots |> List.sumBy _.Weight

/// Prove the two complexity metrics retain their different nested-callable semantics.
let private boundaryTests =
    boundaryCases
    |> List.map (fun (label, language, fixture, functionName, outerScore, innerScore) ->
        testAsync (
            sprintf "%s isolates cyclomatic flow while retaining cognitive nesting" label,
            fun _ ->
                toAsync (
                    task {
                        let! source, tree = parseFixture language fixture
                        assertThat (nodeHasError tree) isFalse

                        let range = findFunctionRange source (FunctionName functionName)

                        let options =
                            { defaultThresholds with
                                Cyclomatic =
                                    {
                                        Enabled = true
                                        MediumThreshold = 1
                                        HighThreshold = 100
                                    }
                                Cognitive =
                                    {
                                        Enabled = true
                                        MediumThreshold = 0
                                        HighThreshold = 100
                                    }
                            }

                        let context = createTestContext source tree language fixture options

                        let cyclomatic =
                            (analyzeFunctionComplexity context).Violations |> violationsIn <| range
                            |> List.filter (fun violation -> violation.Type = Complexity)

                        assertThat cyclomatic.Length (isEqualTo 1)
                        assertThat (List.head cyclomatic).Line (isGreaterThan range.Start)

                        assertThat
                            (List.head cyclomatic).Message
                            (isEqualTo "High cyclomatic complexity: 2. Consider breaking down this function.")

                        let cognitive =
                            (analyzeCognitiveComplexity context).Violations |> violationsIn <| range
                            |> List.filter (fun violation -> violation.Type = Cognitive)

                        assertThat cognitive.Length (isEqualTo 2)

                        let enclosing =
                            cognitive |> List.find (fun violation -> violation.Line = range.Start)

                        let nested = cognitive |> List.find (fun violation -> violation.Line > range.Start)

                        assertThat
                            enclosing.Message
                            (isEqualTo (
                                sprintf
                                    "High cognitive complexity: %d. This function is hard to read; consider flattening nesting or extracting functions."
                                    outerScore
                            ))

                        assertThat
                            nested.Message
                            (isEqualTo (
                                sprintf
                                    "High cognitive complexity: %d. This function is hard to read; consider flattening nesting or extracting functions."
                                    innerScore
                            ))

                        assertThat (hotspotTotal enclosing) (isEqualTo outerScore)
                        assertThat (hotspotTotal nested) (isEqualTo innerScore)
                    }
                )
        ))
    |> fun tests -> testList ("Anonymous callable complexity boundaries", tests)

/// Anonymous parameter forms whose named equivalents are already counted by their adapters.
let private parameterShapeCases =
    [
        "Python defaults",
        Python.pythonLanguageAdapter,
        "python/anonymous_callable_analysis.py",
        "flaggedAnonymousParameterForms"
        "TypeScript defaults, optional, and rest",
        TypeScript.typeScriptLanguageAdapter,
        "typescript/anonymousCallableAnalysis.ts",
        "flaggedAnonymousParameterForms"
        "C++ defaults and variadic pack",
        CPlusPlus.cPlusPlusLanguageAdapter,
        "cpp/anonymous_callable_analysis.cpp",
        "flaggedAnonymousParameterForms"
        "C# defaults",
        CSharp.cSharpLanguageAdapter,
        "csharp/AnonymousCallableAnalysis.cs",
        "FlaggedAnonymousParameterForms"
    ]

/// Confirm grammar-specific default, optional, rest, and variadic syntax stays in the declared count.
let private parameterShapeTests =
    parameterShapeCases
    |> List.map (fun (label, language, fixture, functionName) ->
        testAsync (
            sprintf "%s count six explicit parameters" label,
            fun _ ->
                toAsync (
                    task {
                        let! source, tree = parseFixture language fixture
                        assertThat (nodeHasError tree) isFalse

                        let range = findFunctionRange source (FunctionName functionName)

                        let findings =
                            analyzeFixture source tree language fixture
                            |> fun violations -> violationsIn violations range
                            |> List.filter (fun violation -> violation.Type = Parameters)

                        assertThat findings.Length (isEqualTo 1)
                        assertThat (List.head findings).Severity (isEqualTo Medium)

                        assertThat
                            (List.head findings).Message
                            (isEqualTo (
                                "Parameter explosion: 6 parameters. Consider using objects or builder pattern."
                            ))
                    }
                )
        ))
    |> fun tests -> testList ("Anonymous callable parameter forms", tests)

/// Exercise callable classification and detector-boundary behavior together.
let tests =
    testList ("Anonymous callable analysis", [ classificationTests; boundaryTests; parameterShapeTests ])
