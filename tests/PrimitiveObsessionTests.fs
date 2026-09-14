module Energy.Tests.PrimitiveObsessionTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", Python.pythonLanguageAdapter, "python/primitive_obsession.py"
          "TypeScript", TypeScript.typeScriptLanguageAdapter, "typescript/primitiveObsession.ts"
          "F#", FSharp.fSharpLanguageAdapter, "fsharp/PrimitiveObsession.fs"
          "Kotlin", Kotlin.kotlinLanguageAdapter, "kotlin/PrimitiveObsession.kt"
          "C++", CPlusPlus.cPlusPlusLanguageAdapter, "cpp/primitive_obsession.cpp" ]

    testList (
        "Integration: primitive obsession",
        [ yield!
              cases
              |> List.map (fun (label, language, fixture) ->
                  testAsync (
                      sprintf "%s: flags primitive swap risk and stringly control flow" label,
                      fun _ ->
                          toAsync (
                              task {
                                  let! (source, tree) = parseFixture language fixture
                                  let violations = analyzeFixture source tree language fixture
                                  assertValidPositions violations source
                                  let clean = findFunctionRange source (FunctionName "cleanDistinctTypes")
                                  let swapRisk = findFunctionRange source (FunctionName "flaggedSwapRisk")
                                  let stringly = findFunctionRange source (FunctionName "flaggedStringlyTyped")

                                  let primitiveHits range =
                                      violationsIn violations range
                                      |> List.filter (fun violation -> violation.Type = PrimitiveObsession)

                                  assertThat (primitiveHits clean |> List.length) (isEqualTo 0)

                                  assertThat
                                      (primitiveHits swapRisk
                                       |> List.exists (fun violation -> violation.Message.Contains("swap")))
                                      isTrue

                                  assertThat
                                      (primitiveHits stringly
                                       |> List.exists (fun violation -> violation.Message.Contains("Stringly-typed")))
                                      isTrue
                              }
                          )
                  ))
          // decision: exercises the adapter's direct tuple-membership hook separately because the
          // other grammars deliberately model their membership idioms as unsupported call expressions.
          testAsync (
              "Python: flags a three-value literal membership check",
              fun _ ->
                  toAsync (
                      task {
                          let! (source, tree) =
                              parseFixture Python.pythonLanguageAdapter "python/primitive_obsession.py"

                          let violations =
                              analyzeFixture source tree Python.pythonLanguageAdapter "python/primitive_obsession.py"

                          let membership = findFunctionRange source (FunctionName "flaggedMembershipCheck")

                          assertThat
                              (violationsIn violations membership
                               |> List.exists (fun violation ->
                                   violation.Type = PrimitiveObsession
                                   && violation.Message.Contains("Stringly-typed")))
                              isTrue
                      }
                  )
          )
          testAsync (
              "Python: suppresses pairs that are both keyword-only",
              fun _ ->
                  toAsync (
                      task {
                          let! (source, tree) =
                              parseFixture Python.pythonLanguageAdapter "python/primitive_obsession.py"

                          let violations =
                              analyzeFixture source tree Python.pythonLanguageAdapter "python/primitive_obsession.py"

                          let hits name =
                              violationsIn violations (findFunctionRange source (FunctionName name))
                              |> List.filter (fun violation -> violation.Type = PrimitiveObsession)
                              |> List.length

                          assertThat (hits "suppressedKeywordOnly") (isEqualTo 0)
                          assertThat (hits "suppressedAfterStarArgs") (isEqualTo 0)

                          assertThat
                              (violationsIn
                                  violations
                                  (findFunctionRange source (FunctionName "flaggedPartiallyKeywordOnly"))
                               |> List.exists (fun violation ->
                                   violation.Type = PrimitiveObsession && violation.Message.Contains("swap")))
                              isTrue
                      }
                  )
          )
          // decision: the F# grammar shapes below (and-bindings, postfix types, named-argument calls,
          // match-on-strings) have no equivalent in the other four fixtures, so they get a dedicated
          // F#-only test rather than being added to the shared per-language assertions.
          testAsync (
              "F#: and-bindings, postfix-type barriers, named-arg calls, and string matches",
              fun _ ->
                  toAsync (
                      task {
                          let! (source, tree) = parseFixture FSharp.fSharpLanguageAdapter "fsharp/PrimitiveObsession.fs"

                          let violations =
                              analyzeFixture source tree FSharp.fSharpLanguageAdapter "fsharp/PrimitiveObsession.fs"

                          assertValidPositions violations source

                          let hits (name: string) (message: string) =
                              violationsIn violations (findFunctionRange source (FunctionName name))
                              |> List.filter (fun v -> v.Type = PrimitiveObsession && v.Message.Contains(message))
                              |> List.length

                          // Bug 1 (false positive): an `and`-binding's two heads share the variable name `x`
                          // but each head has fewer than three literals, so neither is stringly-typed.
                          assertThat (hits "cleanAndBindingHead" "Stringly-typed") (isEqualTo 0)
                          assertThat (hits "cleanAndBindingTail" "Stringly-typed") (isEqualTo 0)
                          // Bug 1 (false negative): the second head's swap-risk pair is analyzed.
                          assertThat (hits "flaggedAndBindingPair" "swap") (isEqualTo 1)
                          // Bug 1 (still flagged): a genuinely stringly-typed head is still caught, and its
                          // sibling head (one literal) is not.
                          assertThat (hits "flaggedAndBindingStrings" "Stringly-typed") (isEqualTo 1)
                          assertThat (hits "andBindingStringsTail" "Stringly-typed") (isEqualTo 0)
                          // Bug 2 (postfix-type barrier): `b: string option` separates `a` and `c`, so they
                          // are not a consecutive same-typed pair.
                          assertThat (hits "cleanPostfixTypeBarrier" "swap") (isEqualTo 0)
                          // Bug 3 (named-arg calls): `name = "..."` is a labeled argument, not a comparison.
                          assertThat (hits "cleanNamedArgumentCalls" "Stringly-typed") (isEqualTo 0)
                          // Gap 1 (match on strings): the idiomatic match dispatch is flagged.
                          assertThat (hits "flaggedMatchStringDispatch" "Stringly-typed") (isEqualTo 1)
                      }
                  )
          ) ]
    )
