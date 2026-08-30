# Magic Strings

Flags a string literal only where an unnamed one actually risks a silent typo, deliberately narrower in scope than [magic numbers](magic-numbers.md).

## What it flags

A string literal is a candidate only when it sits at a decision point:

- Compared with `==`/`===`.
- Checked for membership (Python's `x in (...)`).
- Used as a dict/object key or subscript index.

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

## Known limitations

The decision-point scan (equality/membership/dict-key) and the formatted-string exemption are fully implemented for Python, and partially for TypeScript (no `.includes()` membership support yet) and F# (no dict/subscript node, no interpolated-string exemption). See the `LanguageAdapter` fields in `src/Core/LanguageAdapter.fs` for exactly what's modeled per language.

It also doesn't (yet) special-case enum-like keyword/default arguments (e.g. `mode="fast"`) as a lower-confidence decision point, only equality, membership, and dict/index-key positions count.
