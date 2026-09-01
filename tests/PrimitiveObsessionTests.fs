module Energy.Tests.PrimitiveObsessionTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Analyze
open Energy.Languages.Python
open Energy.Languages.TypeScript
open Energy.Languages.FSharp
open Energy.Languages.Kotlin
open Energy.Languages.CPlusPlus
open Energy.Tests.TestUtils

let tests =
    let cases =
        [ "Python", PYTHON, "python/primitiveObsession.py"
          "TypeScript", TYPESCRIPT, "typescript/primitiveObsession.ts"
          "F#", FSHARP, "fsharp/primitiveObsession.fs"
          "Kotlin", KOTLIN, "kotlin/primitiveObsession.kt"
          "C++", CPP, "cpp/primitiveObsession.cpp" ]

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
                                  let violations = analyzeSource source tree language fixture
                                  assertValidPositions violations source
                                  let clean = findFunctionRange source "cleanDistinctTypes"
                                  let swapRisk = findFunctionRange source "flaggedSwapRisk"
                                  let stringly = findFunctionRange source "flaggedStringlyTyped"

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
                          let! (source, tree) = parseFixture PYTHON "python/primitiveObsession.py"
                          let violations = analyzeSource source tree PYTHON "python/primitiveObsession.py"
                          let membership = findFunctionRange source "flaggedMembershipCheck"

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
                          let! (source, tree) = parseFixture PYTHON "python/primitiveObsession.py"
                          let violations = analyzeSource source tree PYTHON "python/primitiveObsession.py"

                          let hits name =
                              violationsIn violations (findFunctionRange source name)
                              |> List.filter (fun violation -> violation.Type = PrimitiveObsession)
                              |> List.length

                          assertThat (hits "suppressedKeywordOnly") (isEqualTo 0)
                          assertThat (hits "suppressedAfterStarArgs") (isEqualTo 0)

                          assertThat
                              (violationsIn violations (findFunctionRange source "flaggedPartiallyKeywordOnly")
                               |> List.exists (fun violation ->
                                   violation.Type = PrimitiveObsession && violation.Message.Contains("swap")))
                              isTrue
                      }
                  )
          ) ]
    )
