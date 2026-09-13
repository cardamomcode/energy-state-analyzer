# Parse, don't validate (ESA-015)

A check can establish a useful fact and then discard it at the function boundary.
Returning a plain list after rejecting empty lists leaves every caller responsible
for remembering that the list is non-empty.

This advisory identifies a narrow opportunity to preserve such facts in domain
values. It is related to [primitive obsession](primitive-obsession.md), but also
applies to collections.

```fsharp
let checkedItems (items: int list) =
    if List.isEmpty items then invalidArg "items" "Must not be empty"
    items
```

Instead, construct a representation that carries the guarantee:

```fsharp
type NonEmptyList<'a> = { Head: 'a; Tail: 'a list }

let parseItems items =
    match items with
    | head :: tail -> Ok { Head = head; Tail = tail }
    | [] -> Error "Must not be empty"
```

The second function's successful result cannot represent an empty list. For other
properties, such as a positive amount, use a private representation and a checked
constructor. A type alias or an unrestricted wrapper alone does not establish the
property. Using exceptions versus `Result` is a separate choice.

## Detection

**Severity: Low.** Enabled by default in Python, F#, TypeScript, Kotlin, C++, and C#.

The rule recognizes a function consisting of:

1. An `if` guard with a single rejecting branch and no alternative.
2. Either a direct return of an explicitly annotated parameter referenced in that
   guard, or no useful success value.

Check-only validators are covered whether they fall through implicitly, use a bare
`return`, or explicitly return Python `None`, TypeScript `undefined`, F# `()`/`None`,
or Kotlin `Unit`. C++ and C# `void` validators are covered too. Simple untyped
parameters are supported for check-only validators.

```python
def validate_items(items):
    if len(items) == 0:
        raise ValueError("Must not be empty")
    # Implicit None: callers receive no value preserving the non-empty guarantee.
```

The diagnostic suggests returning a domain value instead. TypeScript assertion
signatures and constructors are excluded: these can already carry the guarantee
through type narrowing or the constructed instance.

The rejecting branch must consist of a throw/raise statement, or an F# application
of `invalidArg`, `invalidArgf`, `failwith`, `failwithf`, or `raise`. F# implicit
final expressions are supported. Comments and Python docstrings do not count as
executable items. Numeric bounds and non-empty collection checks are covered by the
cross-language fixture matrix.

## Limits

This is a syntax heuristic, not a proof about domain invariants. Review whether the
checked property belongs in the public contract before introducing a domain type.

- Untyped identity returns, multiple guards, `if/else` result expressions,
  `Ok input`, Boolean validators, assertion statements, and indirect validation
  calls are not yet recognized. Arrow functions and lambdas are outside the named-function scan.
- Constructors, transformed returns, intervening statements, and conditional
  throws nested inside the guard are skipped.
- Identity returns with an explicit annotation different from the parameter's
  annotation are skipped: they may represent a refinement the analyzer cannot
  resolve. Check-only validators require an absent or recognized no-value return
  annotation.
- Null checks with identity returns are skipped because language-level narrowing
  can already preserve that information, including the common null-check calls
  (Kotlin `isNullOrEmpty`/`isNull`, C# `IsNullOrEmpty`/`IsNull`). Check-only null
  validators remain candidates because no narrowed value is returned.
- The analyzer does not resolve aliases, infer types, follow callers, prove
  predicate purity, or resolve shadowed F# failure-function names.

## Configuration and suppression

Disable the editor rule with `energyStateAnalyzer.parseDontValidate.enabled`.
The CLI and shared pipeline enable it by default; programmatic callers can set
`AnalyzeOptions.ParseDontValidate.Enabled` to `false`.

For a deliberate compatibility boundary, use a reasoned suppression on the guard:

```fsharp
let checkedItems (items: int list) =
    // Public compatibility API must continue returning an ordinary list.
    // esa-ignore: parse-dont-validate
    if List.isEmpty items then invalidArg "items" "Must not be empty"
    items
```

## Reference

The design principle comes from Alexis King's
[Parse, don't validate](https://lexi-lambda.github.io/blog/2019/11/05/parse-don-t-validate/).
ESA's restricted syntax heuristic is an independent implementation; the article
also covers broader cases this rule does not identify.
