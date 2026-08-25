# Suppression (`esa-ignore`)

A comment directive for silencing a specific violation you've reviewed and decided to accept, without disabling the detector everywhere else. Also flags its own directives once they've gone stale.

## Syntax

```
// esa-ignore
// esa-ignore: nesting
// esa-ignore: nesting, complexity
// esa-ignore-file
// esa-ignore-file: coherence
```

Works with either comment style (`//` or `#`) — the marker text is what matters, not the language's comment syntax.

- **Bare** `esa-ignore` suppresses every violation type on its line.
- **Typed** `esa-ignore: type1, type2` only suppresses the listed types (the same strings the CLI's JSON output uses: `nesting`, `complexity`, `cognitive`, `coherence`, `magic`, `parameters`, `inversion`, `primitive-obsession`, `match-opportunity`, `logical-control-flow`, `opaque-boolean`).
- **`esa-ignore-file`** (bare or typed) can appear anywhere in the file and suppresses that type for the whole file — the only way to suppress `coherence`, which is a file-scoped finding rather than a line-scoped one.

## Placement

A directive suppresses violations on its own line. If it's the *only* thing on its line (nothing before the comment marker), it also covers the line directly below — so it can sit above a function signature or `if` header instead of getting crammed onto an already-long line:

```python
# esa-ignore: complexity
def reconcile_ledger(a, b, c, d, e, f, g, h):
    ...
```

A directive sharing a line with real code only covers that line:

```python
if very_deeply_nested_condition():  # esa-ignore: nesting
    ...
```

## Staying honest

Two situations produce their own low-severity `suppression` finding instead of silently doing nothing:

- **Unused directive** — the violation it named doesn't exist (anymore). Usually means the underlying issue was already fixed and the comment is now dead weight.
- **Unknown type name** — a typo like `esa-ignore: nseting`. An unrecognized type never falls back to "suppress everything"; it matches nothing, which is what surfaces it as unused.

Both show up in the editor and in every CLI report format like any other finding, so a suppression can't quietly outlive the thing it was suppressing.
