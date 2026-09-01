# Cognitive Complexity

Modeled on [SonarSource's metric](https://www.sonarsource.com/resources/cognitive-complexity/). It measures how hard a function is to *read*, so nesting is penalized and straight-line control flow isn't. See [Energy and Entropy](../energy-and-entropy.md) for why this is tracked as a separate score from [cyclomatic complexity](cyclomatic-complexity.md) rather than folded into it.

## What it flags

- Each decision point (`if`, `elif`, `for`, `while`, `except`, ternary, nested named function/method, `lambda`) adds **1 + current nesting depth**.
- `else` adds a flat **+1**, no nesting penalty, since it doesn't add a new branch to reason about.
- Nesting depth only increases when descending into a block body, so an `if` inside two other `if`s scores higher than three sequential `if`s at the top level, even though both have the same cyclomatic complexity.
- Chained boolean operators of the same kind (`a and b and c`) count as a **single** increment rather than one per operator; mixing `and`/`or` starts a new increment.
- A lambda's body complexity is attributed to the enclosing function (lambdas aren't scored as their own function).

This is a simplified first pass on the SonarSource spec, not the full algorithm:

- `for`/`while` `else` clauses (where a grammar has them) are scored like `if`/`else`, even though they aren't really a decision point.
- Boolean-chain merging only checks the immediate parent operator, not the full chain direction.
- Recursive calls to the enclosing function aren't specially detected.
- Match/switch-like constructs and try/except are scored once as a whole, not per-case.

## Example

```python
def handle(items):
    for item in items:          # +1 (nesting 0 -> 1)
        if item.valid:          # +2 (1 + nesting 1)
            if item.ready:      # +3 (1 + nesting 2)
                process(item)
            else:                # +1 (flat, no nesting penalty)
                queue(item)
```

The same four decision points written as flat, sequential `if`s (no nesting) would score far lower here, even though cyclomatic complexity treats both shapes identically.

## Interpreting the score

There's no formal industry consensus the way there is for cyclomatic complexity, since this is a newer, vendor-originated metric, but SonarSource's own convention (and this extension's defaults) treat **15** as the point where a function is hard enough to hold in your head that it's worth splitting up, with scores past 25 or so being seriously hard to follow regardless of how testable the underlying paths are.

The two scores can diverge on the same function: a flat function with many independent branches can have high cyclomatic complexity but modest cognitive complexity (easy to read, hard to test exhaustively), while deeply nested code can be the reverse.

## Configuration

- `energyStateAnalyzer.cognitiveComplexity.mediumThreshold` (default `15`)
- `energyStateAnalyzer.cognitiveComplexity.highThreshold` (default `25`)

A progressive heatmap is also painted across a flagged function's body, mirroring the cyclomatic-complexity heatmap but weighted by nesting-adjusted contribution instead of flat count.

For C++, `switch` is scored once as a nested decision, `catch` clauses contribute as nested
decisions, and C++ lambdas contribute to their enclosing function rather than being analyzed as
standalone functions. Macro-expanded control flow is not visible to this syntax-only analysis.
