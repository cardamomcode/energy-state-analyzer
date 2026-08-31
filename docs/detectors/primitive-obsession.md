# Primitive Obsession

Flags strings and numbers standing in for what should be a distinct, validated type. Two independent sub-checks, both driven through the same per-language traversal.

## What it flags

**Parameter-swap risk.** Two adjacent parameters sharing the same unqualified primitive type (e.g. `lat: float, lon: float`) are indistinguishable at the call site: nothing stops a caller from passing them in the wrong order. Runs on Python, F#, TypeScript, Kotlin, and C++. C++ pointer, reference, array, and function-declarator shapes remain part of the extracted type identity, so `int`, `int*`, and `int&` do not collide.

In Python, a pair is suppressed only when *both* parameters are keyword-only (after a bare `*` or `*args` in the signature), since the signature itself then makes a positional call impossible. Named-parameter naming is still a weaker mitigation than a distinct type (`NewType`, a dataclass, etc.), since nothing stops a future `**kwargs`-splat call from transposing the values by hand, but that gap isn't worth detecting. This suppression doesn't apply to TypeScript, Kotlin, or C++, which have no enforcing keyword-only boundary, or F#, whose named arguments are optional at the call site and so don't prevent a positional call.

**Stringly-typed control flow.** A variable compared against 3 or more distinct string literals within one function is a de facto enum encoded as strings, with no exhaustiveness checking and no typo protection at the type level. Runs on Python, F#, TypeScript, Kotlin, and C++; Python additionally flags a variable checked against a literal tuple/list/set in one `in` expression, since the other adapters have no modeled direct equivalent construct.

## Example

```python
def haversine(lat: float, lon: float, alt: float):
    # flagged: lat/lon and lon/alt are adjacent same-typed pairs a caller can swap
    ...

def handle(status: str):
    if status == "pending": ...
    elif status == "active": ...
    elif status == "closed": ...
    # flagged: 'status' compared against 3 distinct string literals
```

## Known limitations

The `in (a, b, c)`-style membership check for stringly-typed control flow only runs on Python; F#'s grammar has no direct equivalent, TypeScript's idiom (`[...].includes(x)`) is a call expression rather than a comparison node, and common C++ container membership checks are library calls that require semantic resolution. Type aliases and macro-expanded declarations are not resolved.
