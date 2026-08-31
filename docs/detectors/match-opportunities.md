# Match Opportunities

Flags an `if`/`elif`/`elif` chain (or a nested `else if` in TypeScript, Kotlin, or C++) that's really a dispatch on one variable, and would read better as a `match`/`switch` statement.

## What it flags

A chain of 3 or more branches (configurable), all discriminating via equality or membership checks against the *same single variable*, is flagged. An unconditional catch-all `else` at the end still qualifies, since it contributes no discriminant and isn't itself a "branch" for this check, but a chain mixing unrelated conditions across branches does not qualify, since a `match`/`switch` can't express that kind of dispatch anyway.

Runs on Python, F#, TypeScript, Kotlin, and C++.

For C++, only character and integral literals qualify because `switch` does not accept string or
floating-point case values.

## Example

```python
if status == "pending":
    queue()
elif status == "active":
    process()
elif status == "closed":
    archive()
```

All three branches key on `status` against a literal, so this is flagged as a 3-way chain suggesting `match status:` instead.

## Configuration

- `energyStateAnalyzer.matchOpportunity.minBranches` (default `3`), number of branches an if/elif chain must have, all keyed on the same variable, before it's flagged.
