# Parse, don't validate (ESA015)

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
2. A success value that does not carry the checked property: either a direct return
   of an explicitly annotated parameter referenced in that guard, no useful success
   value, or a bare boolean literal.

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

The diagnostic suggests returning a domain value instead. The rejecting branch must
consist of a throw/raise statement, an F# application of `invalidArg`,
`invalidArgf`, `failwith`, `failwithf`, or `raise`, or — for boolean validators — a
single return of a falsy literal (`False`/`false`). F# implicit final expressions are
supported. Comments and Python docstrings do not count as executable items.

Numeric bounds, non-empty collection checks, and boolean validators are covered by
the cross-language fixture matrix.

### Boolean validators

A boolean result carries the verdict, never the value: `bool` is two states with no
payload, so the caller must remember the pairing between the flag and the value. A
function whose success value is a bare boolean literal (either polarity) is flagged
the same way as an identity or absent return.

The F#/Kotlin single-expression form `if condition then false else <success>` is
recognized directly; in the statement grammars an `if`'s branches are blocks, so only
the guard-plus-return form applies. Null checks count: a plain boolean result neither
carries the value nor narrows its type across the call.

```python
def is_valid(pw):
    if "@" not in pw:
        return False
    return True  # flagged: the verdict replaces the value
```

Two shapes stay clean. A guard followed by a fresh boolean computation answers a
question the guard never decided (`if user is None: return False; return
user.role == "admin"`), so only literal-boolean success is flagged. And explicit
narrowing APIs are excluded where the adapter can identify them precisely:
TypeScript assertion signatures and type predicates (`x is T`), Python `TypeGuard`
/`TypeIs` annotations, Kotlin `contract { … }` blocks, C# `[return: NotNullWhen(…)]`,
and constructors. F# and C++ have no such construct; there a null-checking boolean
validator remains a finding.

## Limits

This is a syntax heuristic, not a proof about domain invariants. Review whether the
checked property belongs in the public contract before introducing a domain type.

- Untyped identity returns, multiple guards, `Ok input`, and assertion statements
  are not yet recognized. `if/else` result expressions are recognized only when the
  rejection branch is a bare falsy literal; other rejection branches (for example
  F# `if … then failwith() else …`) are not. Indirect validation calls are
  recognized: a guard whose condition delegates the check to an opaque callee (for
  example `if not valid(amount): raise …`) is still flagged, because the analyzer
  assumes the callee performs the check. Arrow functions and lambdas are outside the
  named-function scan.
- Constructors, transformed returns, intervening statements, and conditional
  throws nested inside the guard are skipped.
- Dispatch and routing guards (a string-equality check with an identity return)
  are flagged like validation guards; the checked property is routing, not a
  domain constraint. Suppress deliberately when the plain value is intentional.
- Identity returns with an explicit annotation different from the parameter's
  annotation are skipped: they may represent a refinement the analyzer cannot
  resolve. Check-only validators require an absent or recognized no-value return
  annotation.
- Null checks with identity returns are skipped because language-level narrowing
  can already preserve that information, including the common null-check calls
  (Kotlin `isNullOrEmpty`/`isNull`, C# `IsNullOrEmpty`/`IsNull`). The exception is
  limited to identity returns: a plain boolean validator over a null check (`if x is
  None: return False; return True`) remains a finding, and check-only null validators
  remain candidates because no narrowed value is returned.
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

Validation layers that are deliberately plain-data boundaries usually triage at
module scope instead: a reasoned file-level directive, `esa-ignore-file` or the
typed `esa-ignore-file: parse-dont-validate`, suppresses the rule for the whole
file rather than every guard.

## Reference

The design principle comes from Alexis King's
[Parse, don't validate](https://lexi-lambda.github.io/blog/2019/11/05/parse-don-t-validate/).
ESA's restricted syntax heuristic is an independent implementation; the article
also covers broader cases this rule does not identify.
