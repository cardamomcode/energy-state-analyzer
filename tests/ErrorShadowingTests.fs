module Energy.Tests.ErrorShadowingTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Core.Config
open Energy.Languages
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/error_shadowing.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/errorShadowing.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/ErrorShadowing.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/error_shadowing.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/error_shadowing.cpp" ]

    testList (
        "Integration: error handling shadows logic",
        (cases
         |> List.map (fun (label, language, fixture) ->
             testAsync (
                 (sprintf "%s: does not flag ordinary protected happy paths" label),
                 fun _ ->
                     toAsync (
                         task {
                             let! (source, tree) = parseFixture language fixture
                             let violations = analyzeFixture source tree language fixture

                             let shadow = findFunctionRange source (FunctionName "shadowedByError")
                             let clean = findFunctionRange source (FunctionName "cleanPath")

                             let hits range =
                                 violationsIn violations range |> List.filter (fun v -> v.Type = ErrorShadowing)

                             // decision: assertValidPositions runs over the whole file's violations, not just
                             // this detector's, so a malformed position from one detector still fails CI.
                             assertThat (hits shadow |> List.length) (isEqualTo 0)
                             assertThat (hits clean |> List.length) (isEqualTo 0)

                             let permissiveOptions =
                                 { defaultAnalyzeOptions with
                                     ErrorShadowing =
                                         { defaultErrorShadowingThresholds with
                                             ProtectedScope =
                                                 { defaultErrorShadowingThresholds.ProtectedScope with
                                                     Threshold = 0.0
                                                     MinItems = 1 } } }

                             let protectedFindings =
                                 createTestContext source tree language fixture permissiveOptions
                                 |> Energy.Core.Detectors.ErrorShadowing.analyzeErrorShadowing
                                 |> _.Violations

                             assertThat
                                 (violationsIn protectedFindings shadow
                                  |> List.filter (fun violation -> violation.Type = ErrorShadowing)
                                  |> List.length)
                                 (isGreaterOrEqual 1)

                             assertValidPositions violations source
                         }
                     )
             )))
        @ [ testAsync (
                "does not flag ordinary recovery inside a nested function",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) =
                                parseFixture Python.pythonLanguageAdapter "python/error_shadowing_nested.py"

                            let violations =
                                analyzeFixture
                                    source
                                    tree
                                    Python.pythonLanguageAdapter
                                    "python/error_shadowing_nested.py"
                                |> List.filter (fun v -> v.Type = ErrorShadowing)

                            assertThat (violations |> List.length) (isEqualTo 0)
                            assertValidPositions violations source
                        }
                    )
            )
            testAsync (
                "flags recovery-dominated functions while leaving their try body as happy-path work",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) =
                                parseFixture Python.pythonLanguageAdapter "python/error_shadowing_recovery_heavy.py"

                            let violations =
                                analyzeFixture
                                    source
                                    tree
                                    Python.pythonLanguageAdapter
                                    "python/error_shadowing_recovery_heavy.py"

                            let dominated = findFunctionRange source (FunctionName "recoveryDominates")

                            assertThat
                                (violationsIn violations dominated
                                 |> List.filter (fun v -> v.Type = ErrorShadowing && v.Severity = High)
                                 |> List.length)
                                (isGreaterOrEqual 1)

                            assertValidPositions violations source
                        }
                    )
            )
            testAsync (
                "reports protected scope, combined modes, and separate try boundaries independently",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) =
                                parseFixture Python.pythonLanguageAdapter "python/error_shadowing_recovery_heavy.py"

                            let options =
                                { defaultAnalyzeOptions with
                                    ErrorShadowing =
                                        { defaultErrorShadowingThresholds with
                                            ProtectedScope =
                                                { defaultErrorShadowingThresholds.ProtectedScope with
                                                    Threshold = 0.4 } } }

                            let violations =
                                createTestContext
                                    source
                                    tree
                                    Python.pythonLanguageAdapter
                                    "python/error_shadowing_recovery_heavy.py"
                                    options
                                |> Energy.Core.Detectors.ErrorShadowing.analyzeErrorShadowing
                                |> _.Violations
                                |> List.filter (fun violation -> violation.Type = ErrorShadowing)

                            let hits name =
                                violationsIn violations (findFunctionRange source (FunctionName name))

                            let broad = hits "broadBoundary"
                            let combined = hits "combinedBoundary"
                            let separate = hits "separateBoundaries"

                            assertThat (broad |> List.length) (isEqualTo 1)
                            assertThat (broad.Head.Message.Contains("protected scope")) isTrue
                            assertThat (combined |> List.length) (isEqualTo 1)
                            assertThat (combined.Head.Message.Contains("recovery/cleanup")) isTrue
                            assertThat (separate |> List.length) (isEqualTo 2)
                            assertValidPositions violations source
                        }
                    )
            )
            testAsync (
                "counts F# finally cleanup as error handling",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) =
                                parseFixture FSharp.fSharpLanguageAdapter "fsharp/ErrorShadowingFinally.fs"

                            let options =
                                { defaultAnalyzeOptions with
                                    ErrorShadowing =
                                        { defaultErrorShadowingThresholds with
                                            Recovery =
                                                { defaultErrorShadowingThresholds.Recovery with
                                                    Threshold = 0.0
                                                    MinItems = 1 } } }

                            let violations =
                                createTestContext
                                    source
                                    tree
                                    FSharp.fSharpLanguageAdapter
                                    "fsharp/ErrorShadowingFinally.fs"
                                    options
                                |> Energy.Core.Detectors.ErrorShadowing.analyzeErrorShadowing
                                |> _.Violations

                            assertThat
                                (violations |> List.filter (fun v -> v.Type = ErrorShadowing) |> List.length)
                                (isGreaterOrEqual 1)
                        }
                    )
            )
            testAsync (
                "does not flag functions without error handling when threshold is zero",
                fun _ ->
                    toAsync (
                        task {
                            let! (source, tree) = parseFixture Python.pythonLanguageAdapter "python/error_shadowing.py"

                            let options =
                                { defaultAnalyzeOptions with
                                    ErrorShadowing =
                                        { defaultErrorShadowingThresholds with
                                            ProtectedScope =
                                                { defaultErrorShadowingThresholds.ProtectedScope with
                                                    Threshold = 0.0 }
                                            Recovery =
                                                { defaultErrorShadowingThresholds.Recovery with
                                                    Threshold = 0.0 } } }

                            let violations =
                                { Source = source
                                  Tree = tree
                                  Language = Python.pythonLanguageAdapter
                                  FileName = "python/error_shadowing.py" }
                                |> analyzeWith options
                                |> _.Violations

                            let clean = findFunctionRange source (FunctionName "cleanPath")

                            assertThat
                                (violationsIn violations clean
                                 |> List.filter (fun v -> v.Type = ErrorShadowing)
                                 |> List.length)
                                (isEqualTo 0)

                            let strictOptions =
                                { options with
                                    ErrorShadowing =
                                        { options.ErrorShadowing with
                                            ProtectedScope =
                                                { options.ErrorShadowing.ProtectedScope with
                                                    Threshold = 1.0 }
                                            Recovery =
                                                { options.ErrorShadowing.Recovery with
                                                    Threshold = 1.0 } } }

                            let strictViolations =
                                { Source = source
                                  Tree = tree
                                  Language = Python.pythonLanguageAdapter
                                  FileName = "python/error_shadowing.py" }
                                |> analyzeWith strictOptions
                                |> _.Violations

                            let shadow = findFunctionRange source (FunctionName "shadowedByError")

                            assertThat
                                (violationsIn strictViolations shadow
                                 |> List.filter (fun v -> v.Type = ErrorShadowing)
                                 |> List.length)
                                (isEqualTo 0)
                        }
                    )
            ) ]
    )
