# Cognitive Complexity

An independent syntax-based implementation modeled on
[SonarSource's metric](https://www.sonarsource.com/resources/cognitive-complexity/).
It estimates the effort of reading control flow through breaks in linear flow and nesting;
it does not directly measure cognition. See [Energy and Entropy](../energy-and-entropy.md)
for why this is tracked separately from [cyclomatic complexity](cyclomatic-complexity.md).

## What it flags

- `if`, loops, ternary expressions, switch/match/when, and each catch/except clause add
  **1 + current nesting depth**. A switch and its cases together receive one structural
  increment; control flow inside a case is nested beneath it.
- `else if`/`elif` and plain `else` add **1**, without a nesting penalty. Their bodies
  still introduce nesting. A braced `else { if (...) ... }` is a nested check, not an `else if`.
- Each sequence of like boolean operators adds **1**. Changing between `and`/`&&` and
  `or`/`||` starts another sequence. Parentheses preserve a sequence; a negated boolean
  group is scored independently. This applies in conditions, assignments, calls, and returns.
- Nested named functions and lambdas add no points for their declarations. Their bodies
  contribute to the enclosing function at an additional nesting level. Named nested functions
  are also analyzed independently; function scores therefore should not be summed as a file total.
- `goto` and labelled `break`/`continue` add **1**. Ordinary breaks, continues, early returns,
  function calls, and null-coalescing/optional-access shorthand add no points themselves.
- `try` and `finally` add no points or nesting. Catch clauses are scored separately, including
  individual F# exception-handler rules; multiple exception types within one clause count once.

Body nesting follows control-flow structure, including single-statement bodies without braces.
The CLI and editor share the same score and positive per-line heatmap contributions.

## Example

```python
def handle(items):
    for item in items:          # +1
        if item.valid:          # +2 (nesting 1)
            if item.ready:      # +3 (nesting 2)
                process(item)
            else:              # +1
                queue(item)
```

This function scores **7**. Three sequential checks with a final `else` would score **4**:
flattening the control flow removes nesting increments.

## Scope and limitations

This implementation does not claim full SonarSource compatibility:

- Direct and indirect recursion cycles are not detected.
- The paper's compensating exceptions for Python decorator wrappers and JavaScript declarative
  outer functions are not implemented; nested function bodies use normal nesting.
- Python loop `else` clauses receive a flat increment and nest their bodies. `try`-`else`
  clauses receive no increment or additional nesting.
- Match/when constructs use one structural increment, including pattern-based dispatch.
- Preprocessor conditionals and macro-expanded control flow are not scored.
- Lambdas and anonymous functions contribute when inside a named function; they are not
  reported as standalone functions. F# mutually recursive heads still share a definition score.

Python and F# have no `do`-`while` syntax. C++ local callables are covered through lambdas,
not nested named function declarations.

## Configuration

Set `cognitiveComplexity.mediumThreshold` (default `15`) and
`cognitiveComplexity.highThreshold` (default `25`) in
[`.esaconfig.json`](../configuration.md). Scores **greater than** 15 report medium severity;
scores **greater than** 25 report high severity with the defaults. These are configurable
review thresholds, not universal boundaries for understandability.

A progressive heatmap is painted across a flagged function's body, weighted by each
contribution to its cognitive score.

## References

- G. Ann Campbell, [*Cognitive Complexity*](https://www.sonarsource.com/resources/cognitive-complexity/),
  SonarSource white paper, version 1.7 (29 August 2023), sections on increments and nesting,
  and Appendices A/B.
