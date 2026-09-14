# Error Boundary Scope

ESA-013 flags an exception boundary that obscures a function's responsibility. It is a cohesion prompt, not a rule against `try`/`catch`: recovery and cleanup are often the right design.

## What it flags

Each `try` is evaluated independently against the logical work in its enclosing function. A logical item is a statement-like body item; identifiers, calls, argument lists, punctuation, and other grammar scaffolding do not inflate the measure.

- **Protected scope** flags a large `try` body. A wide boundary can catch failures from unrelated preparation or follow-up work rather than only the operations it can recover from.
- **Recovery dominance** flags a `catch`, `except`, or `finally` region that occupies too much of the function. Its policy may deserve its own helper or boundary.

When both modes apply to one boundary, ESA-013 emits one finding with both measurements. Multiple `try` regions receive separate findings at their own locations. Nested functions and nested `try` regions are evaluated independently.

## Configuration

`errorShadowing` now has two independently tunable modes in `.esaconfig.json`:

```json
{
  "errorShadowing": {
    "protectedScope": { "threshold": 0.5, "highThreshold": 0.7, "minItems": 8 },
    "recovery": { "threshold": 0.5, "highThreshold": 0.7, "minItems": 5 }
  }
}
```

The matching VS Code settings are `energyStateAnalyzer.errorShadowing.protectedScope.*` and `energyStateAnalyzer.errorShadowing.recovery.*`; `energyStateAnalyzer.errorShadowing.enabled` remains the shared toggle. This replaces the former `threshold`, `highThreshold`, and `minNamedNodes` settings.

## Guidance

Keep the protected region focused on operations whose failures the adjacent handlers can actually recover from. In Python, an `else` clause can keep successful continuation work outside the protected clause. In every language, preserve a deliberate broad boundary when the recovery policy genuinely applies to the whole unit of work; ESA-013 is a review prompt, not proof that extraction is required.

## Known limitations

This is syntax-only analysis. It cannot know which operations throw, whether a handler is reachable, or whether cleanup is operationally essential. It counts direct logical items rather than source lines, so formatting and expression shape do not change the score.
