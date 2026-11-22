# Magic Strings

Flags a string literal only where an unnamed one actually risks a silent typo, deliberately narrower in scope than [magic numbers](magic-numbers.md).

## What it flags

A string literal is a candidate only when it sits at a decision point:

- Compared with `==`/`===`.
- Checked for membership (Python's `x in (...)`).
- Used as a dict/object key or subscript index.
- Used as a match/switch case pattern: Rust `match` arms, `if let`/`while let`, and `matches!` patterns,
  TypeScript and C# `switch` cases (including C# switch expressions and `is` patterns), Python
  `match`/`case`, Kotlin `when` entries, F# `match`/`function` rules, and Erlang `case` clauses. Literals nested in or-, tuple-,
  and constructor patterns such as `Some("a" | "b")` count; guards and arm results don't.

In Rust, a literal wrapped in a single-argument variant constructor is still a comparison operand, so
`kind == Some("analysis")` counts like `kind == "analysis"`. Lowercase calls such as `label("x")` stay
opaque because their argument isn't the compared value.

A message being logged, thrown, or returned isn't a decision point, so it's left alone entirely, as is a docstring. Any f-string, template literal, `.format()`, or `%`-formatted string is exempt too, since a placeholder is itself evidence the string isn't standing in for an enum value. A single-character string is also exempt (too short to plausibly carry hidden meaning).

To cut single-use false positives further, a qualifying literal is only flagged once it recurs at a decision point at least `minDuplicates` times (default `2`) across the file, mirroring SonarSource's S1192 rule.

## Example

```python
def route(status):
    if status == "pending":     # 1st occurrence of "pending" at a decision point
        queue(status)
    if status == "pending":     # 2nd occurrence, now flagged: recurs >= minDuplicates times
        notify(status)
```

## Configuration

- `energyStateAnalyzer.magicString.enabled` (default `true`)
- `energyStateAnalyzer.magicString.minDuplicates` (default `2`)
- `energyStateAnalyzer.magicString.allowlist` (default `["", "utf-8", "__main__"]`)
- Test files and Rust inline test modules are exempt by default, including Rust
  `tests.rs` and `*_tests.rs`; use
  `energyStateAnalyzer.includeTestFiles` or CLI `--include-test-files` to include them.

## Known limitations

Rust macro arguments are unparsed token trees. `matches!` is read token by token (its pattern follows the
first top-level comma and ends at an `if` guard), but a guard's own comparison and other macros such as
`assert_eq!` aren't interpreted. Inequality (`!=`, `<>`, `/=`) isn't modeled in any language. C++ has no case
pattern support because its `switch` labels can't be strings.

The equality/membership/dict-key scan and the formatted-string exemption are fully implemented for Python, and partially for TypeScript (no `.includes()` membership support yet), F# (no dict/subscript node, no interpolated-string exemption), and C++ (equality and subscripts, but no semantic container-membership idiom). C++ raw string literals use a separate grammar node and encoded/prefixed literals are conservatively excluded. See the `LanguageAdapter` fields in `src/Core/LanguageAdapter.fs` for exactly what's modeled per language.

It also doesn't (yet) special-case enum-like keyword/default arguments (e.g. `mode="fast"`) as a lower-confidence decision point, only equality, membership, dict/index-key, and case-pattern positions count.
