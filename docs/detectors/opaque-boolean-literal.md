# Opaque Boolean Literal

Flags a bare `true`/`false` passed positionally into a call, since a reader can't tell what it means without checking the callee's signature.

## What it flags

Unlike [primitive obsession](primitive-obsession.md)'s parameter-swap check, this doesn't need a second adjacent parameter to be a problem: one opaque literal is enough. It's suppressed when the boolean is labeled at the call site, whatever the language allows:

- A Python keyword argument: `configure(retries=True)`.
- A TypeScript object-literal field: `configure({ retries: true })`.
- F#'s named-argument syntax: `configure(retries = true)`.

Unlike the primitive-obsession suppression, F#'s named args count here even though they're optional at the call site, since this rule is about reader comprehension at this specific call, not about preventing a future misuse. Deliberately conservative: only literal `true`/`false` are flagged, not bare `0`/`1`, to avoid noise on ordinary numeric arguments.

## Example

```python
configure(True)                 # flagged: what does True mean here?
configure(retries=True)         # not flagged: labeled at the call site
```

The preferred fix is usually splitting into two clearly named functions (`enable_retries()`/`disable_retries()`) or an enum; naming the argument is an acceptable but weaker mitigation.
