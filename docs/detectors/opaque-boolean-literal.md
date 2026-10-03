# Opaque Boolean Literal

Flags a bare `true`/`false` passed positionally into a call, since a reader can't tell what it means without checking the callee's signature.

## What it flags

Test files are exempt by default: assertions such as `assert_equal(actual, True)` intentionally
pass expected boolean values. Recognition follows the same test-directory and filename rules as
[magic numbers](magic-numbers.md). Enable `energyStateAnalyzer.includeTestFiles` in VS Code or pass
`--include-test-files` to the CLI to include these findings in tests.

Unlike [primitive obsession](primitive-obsession.md)'s parameter-swap check, this doesn't need a second adjacent parameter to be a problem: one opaque literal is enough. It's suppressed when the boolean is labeled at the call site, whatever the language allows:

- A Python keyword argument: `configure(retries=True)`.
- A TypeScript object-literal field: `configure({ retries: true })`.
- F#'s named-argument syntax: `configure(retries = true)`.
- A Kotlin named argument: `configure(retries = true)`.
- A C++ aggregate with a designated field: `configure(Settings{.retries = true})`.

Unlike the primitive-obsession suppression, F#'s named args count here even though they're optional at the call site, since this rule is about reader comprehension at this specific call, not about preventing a future misuse. Deliberately conservative: only literal `true`/`false` are flagged, not bare `0`/`1`, to avoid noise on ordinary numeric arguments.

## Example

```python
configure(True)                 # flagged: what does True mean here?
configure(retries=True)         # not flagged: labeled at the call site
```

The preferred fix is usually splitting into two clearly named functions (`enable_retries()`/`disable_retries()`) or an enum; naming the argument is an acceptable but weaker mitigation.

C++ has no general named-argument syntax, so a direct `configure(true)` remains opaque. Boolean
values created by macros are not visible without preprocessing.
