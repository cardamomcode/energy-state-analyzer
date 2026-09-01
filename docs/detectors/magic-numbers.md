# Magic Numbers

Flags numeric literals used outside of a named binding, an index/key position, or a default parameter value.

## What it flags

Numbers get no free pass for "looking like prose" the way strings do, so this stays broad: any numeric literal not covered by an exemption is a candidate. A literal is exempt when it's:

- In the configured allowlist (see below).
- Bound to a module-level (or explicitly-marked constant) name, e.g. `MAX_RETRIES = 5` at module scope, Kotlin's `const val`, or a C++ `const`/`constexpr` declaration or enumerator.
- Used as an index or subscript key, e.g. `items[0]`.
- A default parameter value, e.g. `def f(retries=3):`.
- In a test file: a `test`/`tests` directory, or a file whose name starts or ends with `test` (e.g. `test_pricing.py`, `pricing_test.ts`, `PricingTest.kt`, `pricing.test.ts`). Other detectors still apply to tests, since tests should stay reasoned-about; this exemption is specific to magic numbers, since literal test inputs and expected values are inherently self-contained.

Negative literals are recognized by structural shape (a unary `-` immediately preceding the literal), so `-1` and `1` are both checked against the allowlist correctly.

### Default allowlist policy

The default allowlist is `[0, 1, -1, 2]`. These are common structural idioms—such as an empty count, a first item, a not-found sentinel, a boolean-like increment, or a two-way choice—where replacing the literal with a named constant usually adds noise rather than clarifying intent.

This is a deliberate usability policy, not a universal definition of a magic number: static-analysis tools and teams choose different exceptions. Values outside this small set remain visible unless they are in an exempt context or a workspace adds them to the allowlist. Configure the allowlist when a domain has additional well-understood literals.

## Example

```python
def calculate_price(base, tier):
    if tier == 1:
        return base * 1.15   # flagged: 1.15 is a magic number
    return base * 1.05       # flagged: 1.05 is a magic number
```

```python
TAX_RATE_STANDARD = 1.05   # not flagged: module-level named constant
```

## Configuration

- `energyStateAnalyzer.magicNumber.enabled` (default `true`)
- `energyStateAnalyzer.magicNumber.allowlist` (default `[0, 1, -1, 2]`), additional values exempt from findings regardless of context. The built-in structural values remain exempt; add domain-specific literals here when their meaning is already clear to the team.

## Known limitations

C++ decimal literals that parse directly as numbers are checked. Hexadecimal/binary literals,
digit separators, and type suffixes such as `42ULL` or `3.0f` are currently skipped rather than
partially interpreted; values introduced through macro expansion are also invisible.
