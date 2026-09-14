# Broad Protected Scope

ESA-013 flags a protected `try` body containing at least 8 logical items and at least 50% of its enclosing function's logical work. Severity is high at 70%; otherwise it is medium. Narrow the protected region to operations whose failures the adjacent handlers can recover from.

Each boundary is evaluated independently and anchored at `try`. Nested functions are evaluated separately. Logical items are direct statements or expressions, including F# handler arms; identifiers, arguments, and punctuation do not inflate the measurement. Nested `try` boundaries are expanded for the function denominator; other control-flow constructs remain one logical item.

The public report and suppression identity remains `error-shadowing` for compatibility. Use `esa-ignore: error-shadowing` with a reason when a broad boundary is deliberate.

## Independent recovery rules

[Recovery Dominance (ESA-016)](recovery-dominance.md) measures recovery's share of a function. [Oversized Recovery Block (ESA-017)](oversized-recovery-block.md) limits each handler or cleanup body. A boundary can produce separate findings for all qualifying rules. Work before or after `try` receives the same treatment; no function designation is required.

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

`energyStateAnalyzer.errorShadowing.enabled` is the VS Code family switch. The individual `energyStateAnalyzer.recoveryDominance.enabled` and `energyStateAnalyzer.oversizedRecoveryBlock.enabled` switches both default to `true`.

## Known limitations

These are syntax-based review prompts. They cannot determine which operations throw, whether a handler is reachable, or whether cleanup is operationally essential. Logical-share checks measure direct work items, while the recovery-block limit intentionally measures source lines.
