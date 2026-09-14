# Recovery Dominance

ESA016 flags a boundary whose combined recovery and cleanup work contains at least 5 logical items and at least 50% of the enclosing function's logical work. Severity is high at 70%; otherwise it is medium. The finding is anchored at `try`.

A protected body with at most one nonblank, noncomment line is exempt. Clause headers and outer block delimiters are excluded. Nested control-flow bodies count, so a multiline loop is not a trivial protected call. Lines containing code alongside comments count; comment-only and blank lines do not.

The exemption depends only on the protected body. Equivalent substantive work before and after `try` produces the same result. Nested functions and boundaries are evaluated independently. Recovery share combines handlers and cleanup; F# retains one logical item per handler arm.

Extract or simplify recovery policy when it obscures the function's work. A legitimate broad recovery policy can use a reasoned `esa-ignore: recovery-dominance`. This suppression leaves [Broad Protected Scope](error-shadowing.md) and [Oversized Recovery Block](oversized-recovery-block.md) active.

## Configuration

Set `errorShadowing.recovery.threshold`, `highThreshold`, and `minItems` in `.esaconfig.json` (defaults `0.5`, `0.7`, and `5`). VS Code's `energyStateAnalyzer.recoveryDominance.enabled` defaults to `true` and operates under the `energyStateAnalyzer.errorShadowing.enabled` family switch.
