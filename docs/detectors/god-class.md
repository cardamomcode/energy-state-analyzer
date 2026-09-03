# God Class

Flags a single class or struct whose methods together solve too many unrelated problems — the
class-level counterpart to [file coherence](file-coherence.md)'s "too many unrelated functions in one
file".

## What it flags

A class with more than 15 methods is considered; past 25 methods it scores high. Consideration alone
is not enough — the detector then measures how *diverse* the methods' types are, reusing the same
type-cohesion signal file coherence uses for function-count sprawl. A class is flagged only when its
methods touch a genuinely wide set of unrelated domain types (a diversity ratio above 0.4), which is
the "too many responsibilities" signal at type granularity.

This is deliberately the mirror image of file coherence's function-count check: there, cohesion
*exempts* a file from flagging; here, diversity *triggers* the flag on a single type.

## Example

```python
class GodService:
    def __init__(self) -> None:
        self.state = []

    def fetch_rows(self, conn: Connection) -> list[Row]: ...   # persistence
    def send_email(self, to: str, body: str) -> bool: ...      # notifications
    def render_pdf(self, data: dict) -> bytes: ...             # reporting
    def resize_image(self, image: Image) -> Image: ...         # imaging
    def validate_token(self, token: Token) -> bool: ...        # auth
    # ...and so on, each method a different concern
```

Its methods span persistence, email, PDF, imaging, and auth — unrelated types with no shared domain,
so the class is flagged as a god class. The message names how many methods and distinct types it
spans and suggests splitting it, or confirming the methods are one cohesive API (see below).

## Cohesive value types are not god classes

A stateless value type with a rich but *cohesive* API is **not** a god class — even when it has more
methods than the count bar. An `Option[T]`-style tagged union of pure combinators (`map`, `bind`,
`filter`, `or_else`, …) has every method transform a single domain type, so its type-diversity ratio
stays low and it is not flagged. This covers the common case of a module-like value type used for
method chaining: many methods over one type is cohesion, not sprawl.

The same rule also lets through a class whose methods are all static (a namespace of functions — that
is function-count sprawl's concern, not this one) and fluent/builder-style classes whose methods all
return the same value type (one responsibility: producing a derived value).

## Known limitations

- The method-count bars (15 medium / 25 high) are fixed in code. The type-diversity ratio is
  configurable through the existing [file-coherence configuration](file-coherence.md), since this
  check reuses that signal.
- Type diversity is measured from declared parameter and return annotations, so it under-counts for
  weakly-typed code: when too few methods carry any annotation, the check stays quiet rather than
  guessing (conservative, to avoid false positives). Explicit string/forward-reference annotations
  (Python's `"Connection"`) are not parsed as types and so do not contribute a signal.
- C++ pointer/reference return and parameter shapes (`Connection*`) are treated as distinct from the
  bare type and are excluded from the diversity measurement by the shared type helper.
