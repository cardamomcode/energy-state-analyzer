module Energy.Tests.SuppressionsTests

open Scriptorium.Quill
open Scriptorium.Nib.Assertion
open type Scriptorium.Quill.Test
open Energy.Core.Violation
open Energy.Core.Suppressions

let private violation line violationType =
    { Line = line
      Column = 0
      Type = violationType
      Severity = Medium
      Message = "test"
      Hotspots = [] }

let tests =
    testList (
        "Integration: esa-ignore suppressions",
        [ test (
              "bare same-line directive suppresses every type",
              fun _ ->
                  let result = applySuppressions [ violation 0 Nesting ] "if deep(): # esa-ignore\n"
                  assertThat result.Violations.Length (isEqualTo 0)
                  assertThat result.SuppressionNotes.Length (isEqualTo 0)
          )
          test (
              "typed directive only suppresses listed types",
              fun _ ->
                  let result =
                      applySuppressions
                          [ violation 0 Nesting; violation 0 Complexity ]
                          "if deep(): // esa-ignore: nesting\n"

                  assertThat result.Violations.Length (isEqualTo 1)
                  assertThat result.Violations.Head.Type (isEqualTo Complexity)
          )
          test (
              "standalone directive covers its next line",
              fun _ ->
                  let result =
                      applySuppressions [ violation 1 Complexity ] "// esa-ignore: complexity\nfunction big() {}\n"

                  assertThat result.Violations.Length (isEqualTo 0)
          )
          test (
              "trailing directive does not cover its next line",
              fun _ ->
                  let result =
                      applySuppressions
                          [ violation 1 Complexity ]
                          "const x = 1 // esa-ignore: complexity\nfunction big() {}\n"

                  assertThat result.Violations.Length (isEqualTo 1)
          )
          test (
              "file directive covers all matching violations",
              fun _ ->
                  let result =
                      applySuppressions
                          [ violation 0 Coherence; violation 40 Coherence ]
                          "# esa-ignore-file: coherence\n"

                  assertThat result.Violations.Length (isEqualTo 0)
          )
          test (
              "unused directives become suppression findings",
              fun _ ->
                  let result = applySuppressions [] "return 1 // esa-ignore: magic\n"
                  assertThat result.SuppressionNotes.Length (isEqualTo 1)
                  assertThat result.SuppressionNotes.Head.Type (isEqualTo Suppression)
                  assertThat result.SuppressionNotes.Head.Severity (isEqualTo Low)
                  assertThat (result.SuppressionNotes.Head.Message.Contains("Unused")) isTrue
          )
          test (
              "unknown types produce unknown and unused notes",
              fun _ ->
                  let result =
                      applySuppressions [ violation 0 Nesting ] "return 1 # esa-ignore: nseting\n"

                  assertThat result.SuppressionNotes.Length (isEqualTo 2)

                  assertThat
                      (result.SuppressionNotes
                       |> List.exists (fun note -> note.Message.Contains("unknown")))
                      isTrue
          )
          test (
              "recognizes slash and hash directives",
              fun _ ->
                  let suppressions = parseSuppressions "a // esa-ignore\nb # esa-ignore-file: magic\n"
                  assertThat suppressions.Length (isEqualTo 2)
                  assertThat suppressions.Head.Scope (isEqualTo Line)
                  assertThat suppressions.Tail.Head.Scope (isEqualTo File)
                  assertThat suppressions.Tail.Head.Types (isEqualTo (Some(Set.singleton Magic)))
          )
          test (
              "prose and malformed markers are not directives",
              fun _ ->
                  let source =
                      "//esa-ignore marker text\nconst x = 1 #esa-ignore nesting extra words\n"

                  let result = applySuppressions [ violation 1 Nesting ] source
                  assertThat (parseSuppressions source).Length (isEqualTo 0)
                  assertThat result.Violations.Length (isEqualTo 1)
          ) ]
    )
