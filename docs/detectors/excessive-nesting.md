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

The medium/high thresholds default to `3` / `5` and are configured in a project's [`.esaconfig.json`](../configuration.md), so the editor and CLI/CI share them. CLI flags (`--medium-nesting`, `--high-nesting`) can temporarily override them for one scan.

C++ code produced by macro expansion is not present in the syntax tree and therefore cannot add to
the measured depth.
