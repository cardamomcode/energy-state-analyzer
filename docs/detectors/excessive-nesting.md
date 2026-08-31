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

## Known limitations

Thresholds are not yet exposed as VS Code settings, unlike most other detectors. The medium/high thresholds (3/5) are currently fixed; they can only be overridden when using the [CLI](../cli.md) directly (`--medium-nesting`, `--high-nesting`).

C++ code produced by macro expansion is not present in the syntax tree and therefore cannot add to
the measured depth.
