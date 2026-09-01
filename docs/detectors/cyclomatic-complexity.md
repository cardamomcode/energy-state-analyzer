# Cyclomatic Complexity

Counts the number of independent paths through a function. Flags functions that have too many execution paths to test exhaustively, regardless of how those paths are arranged.

## What it flags

Starting from a base of **1**, every decision point adds **+1**, no matter how deeply it's nested:

- `if` / `elif` / `while` / `for` / `except`/`catch` (including C++ range loops and `do`)
- `and` / `or` (a chain of the same operator still counts once per operator here, unlike cognitive complexity's chain merging)
- ternary (`a if cond else b`)
- match/switch-like constructs, using their actual arm count rather than a flat `+1`; a switch with
  no fallback also includes its implicit unmatched path

A nested named function or method is scored separately, as its own violation, never folded into the enclosing function's count.

Two functions with the same number of `if`s score the same whether those `if`s are sequential or nested five deep. This metric measures *how many paths exist*, not how hard the code is to follow, that's what [cognitive complexity](cognitive-complexity.md) is for. See [Energy and Entropy](../energy-and-entropy.md) for why the two are tracked separately.

## Example

```python
def classify(status, region, tier, flag):
    if status == "active":
        if region == "eu" and tier == "gold":
            pass
        elif region == "us" or flag:
            pass
    elif status == "pending":
        if tier == "silver":
            pass
    # ... continues for many more branches
```

Each `if`/`elif`/`and`/`or` above adds one to the count, on top of the base of 1.

## Interpreting the score

McCabe's original 1976 paper proposed risk bands that are still the closest thing to an industry consensus (echoed by SonarQube, ESLint's `complexity` rule, and NIST guidance):

| Score | Risk | Roughly |
| --- | --- | --- |
| 1-10 | Low | Simple, easy to test exhaustively |
| 11-20 | Moderate | Getting harder to cover with tests |
| 21-50 | High | Complex, testing all paths is impractical |
| 50+ | Very high | Effectively untestable |

## Configuration

- `energyStateAnalyzer.cyclomaticComplexity.mediumThreshold` (default `10`)
- `energyStateAnalyzer.cyclomaticComplexity.highThreshold` (default `15`)

A progressive heatmap is also painted across a flagged function's body: each contributing line is shaded by how much it drives up the score relative to that function's own worst line, so you can see which branches to break apart first.

For C++, this is a syntax metric: preprocessor branches and control flow introduced by macro
expansion are not counted.
