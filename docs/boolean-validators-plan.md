# Boolean Validator Detection Plan

Extend parse-don't-validate (ESA015) to flag boolean validators: functions that
establish a property on a parameter and return a bare boolean instead of a value
carrying the property. The detector's own documentation already names this gap —
the Limits section of `docs/detectors/parse-dont-validate.md` lists "Boolean
validators" among the shapes "not yet recognized".

## Findings

### The information loss

A boolean result carries the verdict, never the value. The caller writes

```python
if is_valid(pw):
    use(pw)  # pw is still the raw string; the invariant lives in the caller's head
```

and has to remember the pairing between the flag and the value. That is the same
class of information loss ESA015 already reports for `void`/`None` success
returns: a `bool` is a glorified `Option<unit>` — two states (pass/fail), no
payload. Where the rule currently sees two leak shapes (the checked value returned
unchanged, or no useful value returned at all), a boolean validator is a third:
the checked value is replaced by its verdict.

### Why boolean validators are invisible today

Two separate doors are closed:

1. **Bool-rejecting guards are never extracted.** `ValidationSyntax.rejects` only
   accepts throw/raise/fail branches as rejections. A guard that fails by
   `return False` is not a rejection, so the function never becomes a candidate.
2. **Throwing guard plus boolean success is extracted but skipped.**
   `if pw is None: raise; return True` yields `UnchangedInput(True)`, but
   `checkedParameter` only flags identity returns (success text equal to the
   parameter name), so a literal `True` falls through unreported.

F#'s single-expression spelling adds a third gap: `if cond then false else true`
parses as a three-child `if_expression` (condition, then, else), and `extract`
only matches the two-child guard shape. That is the "if/else result expressions"
item in the same Limits list.

## Intended semantics

### Flagged shapes

A function is a boolean validator when its body is a single rejecting guard, the
guard's condition references a parameter, and the success value is a boolean
literal. The rejection is either:

- a throw/raise/fail branch (the existing rejection), or
- a single return of a falsy boolean literal (`False`/`false`) — a bool-rejecting
  guard.

```python
def flagged_boolean_validator(pw: str) -> bool:
    if "@" not in pw:
        return False
    return True          # flagged: the verdict replaces the value

def flagged_throwing_boolean(pw: str) -> bool:
    if pw is None:
        raise ValueError("missing")
    return True          # flagged: the True is noise over an established fact
```

F#'s expression form `if condition then false else <success>` is recognized
directly through the three-branch shape. There is no equivalent two-item F# form:
an `if` without an `else` must return `unit`, so `if condition then false`
followed by `true` is invalid F#. A throwing guard can still use the two-item form
because `failwith`/`raise` can be inferred as `unit` in that position.

Null checks are included. A plain `bool` result from `is_not_null(x)` does not
carry `x` or narrow its type across the call, so `if x is None: return False;
return True` has the same information loss as every other boolean validator. The
existing `rejectsNull` exception remains limited to identity returns, where the
returned value itself can preserve language-level narrowing.

### Clean shapes (scope calls)

- **Fresh-computation success.** `if user is None: return False; return
  user.role == "admin"` answers a question the guard never decided; that is a
  query (`is_admin`), and the boolean is the answer. Only literal-boolean success
  is flagged.
- **Explicit narrowing APIs.** A specialized signature or contract that carries
  narrowing across the call is not a plain boolean validator. Examples include a
  TypeScript type predicate, Python `TypeGuard`/`TypeIs`, a Kotlin contract, or a
  C# nullability annotation such as `NotNullWhen`. Exempt a form only when the
  adapter can identify that construct explicitly; never infer narrowing from a
  `bool` return or a null-check condition alone. Any such exemption needs a named
  negative fixture, and unsupported contract systems stay documented rather than
  gaining a broad null-based escape hatch.
- **Flipped branches.** `if x is not None: return True; return False` returns a
  truthy literal from the guard branch. By convention (`is_`/`has_`/`can_` return
  true on success) only a falsy literal is a rejection, so these stay unextracted.

### Model changes

- `ValidationSuccess` gains a `BareBoolean of Node` case; `success` classifies a
  boolean-literal success value (either polarity) instead of `UnchangedInput`.
- `GuardSyntax` gains one narrow predicate (exact name to settle in
  implementation), for example `BooleanLiteralValue: Node -> bool option`
  returning the literal's polarity — read as `Some false` in `rejects` and
  `Some _` in `success`. It follows the existing `IsNonExecutable` pattern of
  passing a small predicate through `GuardSyntax` rather than the full adapter.
  Per-grammar literal shapes: Python/TypeScript/C++ `true`/`false` nodes, C#
  `boolean_literal`, F# `const` wrapping a `bool` child, Kotlin `identifier` with
  `true`/`false` text (matching each language's existing `IsBooleanLiteral` hook).
- `rejects` accepts a single return statement whose value is `Some false`.
- `extract` gains a three-branch match `[condition; then; else]` where the then
  branch is `Some false`. The predicate guard makes the shape safe in statement
  grammars: their `if` children are blocks or else-clauses, never bare literals,
  so only F#'s bare-expression branches can match.
- `checkedParameter` gains a `BareBoolean` branch with a third loss phrase —
  "success returns only a boolean" — alongside "success returns no useful value"
  and "returned unchanged". No new `ViolationType`, no configuration change, no
  presentation change: ESA015 stays Low and default-on in the same six languages.

## Phases

### Phase 1: Shared syntax

`src/Core/LanguageAdapter.fs` (new `ValidationSuccess` case) and
`src/Languages/ValidationSyntax.fs` (`GuardSyntax` field, `rejects`, `success`,
three-branch `extract`).

### Phase 2: Language adapters

Wire the boolean-literal predicate in all six adapters (Python, TypeScript, F#,
Kotlin, C++, C#). The literal handling is mechanical per language given the node
shapes above. Keep existing preserved-information exclusions and add an exclusion
only where a specialized narrowing construct can be identified precisely and
covered by a negative fixture.

### Phase 3: Detector

`src/Core/Detectors/ParseDontValidate.fs`: the `BareBoolean` branch and the
message. Do not apply `rejectsNull` to this branch.

### Phase 4: Fixture parity

Per AGENTS.md, named scenarios in every language fixture plus a shared row in
`tests/DetectorFixtureMatrixTests.fs`:

- `flaggedBooleanValidator` — non-null condition, falsy rejection, literal success
  → Low;
- `flaggedThrowingBoolean` — throwing guard, literal success → Low;
- `flaggedNullBooleanValidator` — null check, falsy rejection, literal success →
  Low;
- `cleanBooleanQuery` — guard plus fresh boolean computation → clean;
- `cleanExplicitNarrowingValidator` — a recognized narrowing signature or
  contract → clean, with a per-language clean limitation where no such construct
  is supported by this detector.

Edge cases are named fixture scenarios with matrix rows rather than inline test
sources: flipped branches (`cleanFlipped`, shared row), both null comparison
polarities (`flaggedNullBooleanValidator` and the per-language
`flaggedInvertedNullBooleanValidator`), the F# throwing-then if/else form staying
unextracted (documented limitation, `cleanThrowingThen`), and Kotlin's
expression-form if (`flaggedExpressionIf`). Both positive and negated null
comparisons are findings when their rejection branch is falsy and their success is
a boolean literal; `rejectsNull`'s operator list remains relevant only to existing
identity-return behavior.

Where a spelling diverges per language, the matrix row carries a per-language
override stating the unsupported semantic explicitly instead of silently omitting
it.

### Phase 5: Documentation and validation

- `docs/detectors/parse-dont-validate.md`: move "Boolean validators" out of the
  not-yet-recognized Limits list into Detection; narrow the "if/else result
  expressions" Limit to rejection branches that are not falsy literals (for
  example F# `if … then failwith() else …`); clarify that the null-check exception
  applies to identity returns, while plain `is_not_null`-style boolean validators
  are findings; note the query and explicit-narrowing exclusions.
- `just format`, `just lint`, `just md-lint`, `just analyze`; triage every new
  finding, including any the analyzer surfaces in our own F# source under the new
  branch.

## Delivery sequence

One PR is the expected size. If review load warrants, split as:

1. Core detection: shared syntax, adapters, detector, F# fixtures and tests.
2. Cross-language parity: remaining five fixtures, matrix rows, docs.

## Completion criteria

- Both positive shapes (bool-rejecting and throwing plus literal success) flag in
  all six languages, including plain nullable-parameter validators.
- Fresh-computation and flipped-branch shapes stay clean in all six languages;
  every specialized narrowing exemption is explicit and fixture-backed.
- Matrix rows and the dedicated edge tests pass; `just analyze` is clean or
  triaged.
- "Boolean validators" no longer appears in the detector's Limits list.

## Rough estimate

Small-to-medium. One narrow change in the shared syntax module, six literal
predicate adapter wirings plus precise narrowing exclusions where supported, one
detector branch, six fixture files, five shared matrix rows plus a handful of edge
tests, and a documentation update. Roughly a day of implementation plus triage.
