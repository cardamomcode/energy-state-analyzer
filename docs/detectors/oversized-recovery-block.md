# Oversized Recovery Block

ESA-017 flags each individual handler or cleanup body exceeding 20 nonblank, noncomment source lines, at medium severity. Exactly 20 lines stays clean; 21 triggers. This check applies even when the protected body is trivial or recovery occupies only a small share of the function.

Separate catch handlers, F# handler arms, and finally bodies have separate limits and findings. They are never combined for this limit. Findings are anchored at the relevant handler or cleanup clause. C++ has catch handlers but no finally clause.

Clause headers, outer block delimiters, blank lines, and parsed comment-only lines are excluded. A line containing both code and comments counts. Nested control-flow and nested function bodies remain part of the containing body's source-line size.

Extract or simplify the oversized handler or cleanup body. Use a reasoned `esa-ignore: oversized-recovery-block` at its clause for a deliberate exception; other error-boundary rules remain active.

## Configuration

Set `errorShadowing.recoveryBlock.maxLines` in `.esaconfig.json` (default `20`). VS Code's `energyStateAnalyzer.oversizedRecoveryBlock.enabled` defaults to `true` and operates under the `energyStateAnalyzer.errorShadowing.enabled` family switch. Thresholds are project configuration, not editor settings.
