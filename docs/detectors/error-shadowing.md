# Broad Protected Scope

ESA013 flags a protected `try` body containing at least 8 logical items and at least 50% of its enclosing function's logical work. Severity is high at 70%; otherwise it is medium. Narrow the protected region to operations whose failures the adjacent handlers can recover from.

Each boundary is evaluated independently and anchored at `try`. Nested functions are evaluated separately. Logical items are direct statements or expressions, including F# handler arms; identifiers, arguments, and punctuation do not inflate the measurement. Nested `try` boundaries are expanded for the function denominator; other control-flow constructs remain one logical item.

The public report and suppression identity remains `error-shadowing` for compatibility. Use `esa-ignore: error-shadowing` with a reason when a broad boundary is deliberate.

## Independent recovery rules

[Recovery Dominance (ESA016)](recovery-dominance.md) measures recovery's share of a function. [Oversized Recovery Block (ESA017)](oversized-recovery-block.md) limits each handler or cleanup body. A boundary can produce separate findings for all qualifying rules, including separate ESA013 and ESA016 findings when both logical-share checks qualify. Multiple `try` regions receive separate findings at their own locations. Nested functions and nested `try` regions are evaluated independently.

## Configuration

Thresholds belong in `.esaconfig.json`:

```json
{
  "errorShadowing": {
    "protectedScope": { "threshold": 0.5, "highThreshold": 0.7, "minItems": 8 },
    "recovery": { "threshold": 0.5, "highThreshold": 0.7, "minItems": 5 },
    "recoveryBlock": { "maxLines": 20 }
  }
}
```

`energyStateAnalyzer.errorShadowing.enabled` is the VS Code family switch. The individual `energyStateAnalyzer.recoveryDominance.enabled` and `energyStateAnalyzer.oversizedRecoveryBlock.enabled` switches both default to `true`. The matching protected-scope and recovery thresholds live under `energyStateAnalyzer.errorShadowing.protectedScope.*` and `energyStateAnalyzer.errorShadowing.recovery.*`; this replaces the former `threshold`, `highThreshold`, and `minNamedNodes` settings.

## Guidance

Keep the protected region focused on operations whose failures the adjacent handlers can actually recover from. In Python, an `else` clause can keep successful continuation work outside the protected clause. In every language, preserve a deliberate broad boundary when the recovery policy genuinely applies to the whole unit of work; ESA013 is a review prompt, not proof that extraction is required.

## Known limitations

These are syntax-based review prompts. They cannot determine which operations throw, whether a handler is reachable, or whether cleanup is operationally essential. Logical-share checks measure direct work items, while the recovery-block limit intentionally measures source lines.
