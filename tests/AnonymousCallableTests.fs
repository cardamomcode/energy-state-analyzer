module Energy.Tests.AnonymousCallableTests

open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test

open Energy.Core.LanguageAdapter
open Energy.Core.TreeSitter
open Energy.Languages
open Energy.Tests.CallableTestUtils
open Energy.Tests.TestUtils

/// Expected role, direct binding name, and explicit parameter count for one callable.
type private CallableExpectation = CallableRole * string option * int

/// Cross-language callable scenarios in their expected source traversal order.
let private cases =
    [ "Python",
      Python.pythonLanguageAdapter,
      "python/anonymous_callables.py",
      [ NamedDefinition, Some "named", 1
        BoundAnonymous ModuleBinding, Some "module_bound", 2
        BoundAnonymous ClassMemberBinding, Some "class_bound", 1
        NamedDefinition, Some "outer", 1
        InlineAnonymous, Some "local_bound", 1
        InlineAnonymous, None, 1 ]
      "TypeScript",
      TypeScript.typeScriptLanguageAdapter,
      "typescript/anonymousCallables.ts",
      [ NamedDefinition, Some "named", 1
        BoundAnonymous ModuleBinding, Some "moduleArrow", 2
        BoundAnonymous ModuleBinding, Some "moduleFunction", 1
        BoundAnonymous ClassMemberBinding, Some "classBound", 1
        NamedDefinition, Some "namedMethod", 1
        NamedDefinition, Some "outer", 1
        InlineAnonymous, Some "localBound", 1
        InlineAnonymous, None, 1 ]
      "F#",
      FSharp.fSharpLanguageAdapter,
      "fsharp/AnonymousCallables.fs",
      [ NamedDefinition, Some "named", 1
        NamedDefinition, Some "mutualFirst", 1
        NamedDefinition, Some "mutualSecond", 2
        BoundAnonymous ModuleBinding, Some "moduleBound", 2
        BoundAnonymous ClassMemberBinding, Some "ClassBound", 1
        NamedDefinition, Some "outer", 1
        InlineAnonymous, Some "localBound", 1
        InlineAnonymous, None, 1 ]
      "Kotlin",
      Kotlin.kotlinLanguageAdapter,
      "kotlin/AnonymousCallables.kt",
      [ NamedDefinition, Some "named", 1
        BoundAnonymous ModuleBinding, Some "moduleBound", 2
        BoundAnonymous ClassMemberBinding, Some "classBound", 1
        NamedDefinition, Some "namedMethod", 1
        NamedDefinition, Some "outer", 1
        InlineAnonymous, Some "localBound", 1
        InlineAnonymous, None, 0 ]
      "C++",
      CPlusPlus.cPlusPlusLanguageAdapter,
      "cpp/anonymous_callables.cpp",
      [ NamedDefinition, Some "named", 1
        BoundAnonymous ModuleBinding, Some "moduleBound", 2
        BoundAnonymous ClassMemberBinding, Some "classBound", 1
        NamedDefinition, Some "namedMethod", 1
        NamedDefinition, Some "outer", 0
        InlineAnonymous, Some "localBound", 1
        InlineAnonymous, None, 1 ]
      "C#",
      CSharp.cSharpLanguageAdapter,
      "csharp/AnonymousCallables.cs",
      [ BoundAnonymous ClassMemberBinding, Some "ClassBound", 2
        BoundAnonymous ClassMemberBinding, Some "ClassMethod", 1
        NamedDefinition, Some "NamedMethod", 1
        NamedDefinition, Some "Outer", 1
        InlineAnonymous, Some "localBound", 1
        InlineAnonymous, None, 1 ] ]

/// Verify the shared callable contract against every bundled grammar.
let tests =
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
