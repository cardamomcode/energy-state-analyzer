# Cyclomatic Complexity

Counts independent paths through a function's control-flow graph and flags functions with
too many decisions under the configured thresholds. More independent paths increase the
work needed for structural testing, regardless of how deeply the decisions are nested.

## What it flags

Starting from a base of **1**, every decision point adds **+1**, no matter how deeply it's nested:

- `if` / `elif` / `while` / `for` / `except`/`catch` (including C++ range loops and `do`)
- `and` / `or` (a chain of the same operator still counts once per operator here, unlike cognitive complexity's chain merging)
- ternary (`a if cond else b`)
- match/switch-like constructs, using their actual arm count rather than a flat `+1`; a switch with
  no fallback also includes its implicit unmatched path

A nested named function or method is scored separately, as its own violation, never folded into the enclosing function's count.

Two functions with the same number of `if`s score the same whether those `if`s are sequential or nested five deep. This metric measures *independent control-flow paths*, rather than every possible execution.
[Cognitive complexity](cognitive-complexity.md) estimates reading effort through breaks in
linear flow and nesting. See [Energy and Entropy](../energy-and-entropy.md) for why the two are tracked separately.

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

Cyclomatic complexity describes the size of a basis of independent control-flow paths.
It can guide structural testing, but does not supply a complete test count or establish
that all paths are feasible. A loop can produce many execution traces without increasing
the metric for each iteration. Input values, contracts, and interactions still need review.

When a function is flagged, simplify redundant conditions and separate independent
responsibilities into operations that can be understood and tested independently. Use the
reported hotspots to locate contributing decisions, then verify the affected branches and
boundary cases. Moving decisions into arbitrary helpers merely to lower the count leaves
the original reasoning burden in place.

The CLI human report maps complexity values onto the familiar CVSS 0.0–10.0 severity bands,
with guidance from keeping changes small at Low to restructuring extremely complex code
at Critical. This is the analyzer's complexity score, not a security vulnerability score
or proof that code is untestable. See [the human report](../cli.md#a-report-for-humans---report-human)
for its scale. Detector thresholds remain project-configurable; the report curve is fixed.

## Configuration

Set `cyclomaticComplexity.mediumThreshold` (default `10`) and
`cyclomaticComplexity.highThreshold` (default `15`) in
[`.esaconfig.json`](../configuration.md). The editor and CLI/CI use the same
project thresholds.

A progressive heatmap is also painted across a flagged function's body: each contributing line is shaded by how much it drives up the score relative to that function's own worst line, so you can see which branches to break apart first.

For C++, this is a syntax metric: preprocessor branches and control flow introduced by macro
expansion are not counted.

## References

- Thomas J. McCabe, “[A Complexity Measure](https://doi.org/10.1109/TSE.1976.233837),” *IEEE Transactions on Software Engineering*, SE-2(4), 308–320 (1976).
- D. Wallace, A. H. Watson, and T. J. McCabe, [*Structured Testing: A Testing Methodology Using the Cyclomatic Complexity Metric*](https://www.nist.gov/publications/structured-testing-testing-methodology-using-cyclomatic-complexity-metric), NIST Special Publication 500-235 (1996).
