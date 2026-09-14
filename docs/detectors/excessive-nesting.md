# Excessive Nesting

Flags control-flow blocks nested deeper than a reader can comfortably track.

## What it flags

Control-flow blocks (`if`, loops, `try`, and match/switch-like constructs, whichever a language's grammar has) nested more than 3 levels deep are flagged as medium severity; past 5 levels deep, severity escalates to high. C++ includes classic and range `for`, `while`, `do`, `try`, and `switch`. The default medium threshold of 3 is the point where tracking active conditions starts to strain working memory.

## Example

```python
def process(orders):
    for order in orders:          # depth 0
        if order.active:          # depth 1
            for item in order.items:   # depth 2
                if item.in_stock:      # depth 3
                    if item.discounted:  # depth 4, flagged (medium)
                        apply_discount(item)
```

## How this differs from cognitive complexity

Both detectors account for nesting, but they answer different questions:

- Excessive nesting asks how many control-flow scopes a reader must keep active at the deepest
  point. It reports each block beyond the configured depth, including blocks outside named
  functions.
- [Cognitive complexity](cognitive-complexity.md) estimates the total reading effort accumulated
  across a named function. It combines structural flow and nesting with other disruptions such as
  `else`, boolean-operator sequences, catch clauses, and labelled jumps, then reports the function
  when its total score crosses the configured threshold.

This makes excessive nesting a focused maximum-depth guard rather than a second aggregate
complexity score. For example, five directly nested structural controls have cognitive increments
of `1 + 2 + 3 + 4 + 5 = 15`. That does not exceed the default cognitive threshold of 15, but the
fifth control is at depth 4 and is therefore an excessive-nesting finding with the default nesting
threshold of 3.

The measures also differ for some constructs. Excessive nesting counts `try` scopes and Python
`with` scopes. Cognitive complexity gives `try` and `finally` no increment or additional nesting,
but scores each catch clause instead. Conversely, cognitive complexity accounts for boolean
operator sequences, `else`, ternary expressions, and labelled jumps, none of which increase the
excessive-nesting depth by themselves.

## Known limitations

The medium/high thresholds default to `3` / `5` and are configured in a project's [`.esaconfig.json`](../configuration.md), so the editor and CLI/CI share them. CLI flags (`--medium-nesting`, `--high-nesting`) can temporarily override them for one scan.

C++ code produced by macro expansion is not present in the syntax tree and therefore cannot add to
the measured depth.
