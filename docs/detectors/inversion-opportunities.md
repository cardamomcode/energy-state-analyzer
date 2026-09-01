# Inversion Opportunities

Flags patterns that could be rewritten as guard clauses with early returns, rather than nested conditional logic.

## What it flags

Three independent patterns, checked per function:

1. **Dominant if-block.** The function's first statement is an `if` whose body spans more than half the function's total length. A large `if` that dominates a function this way is usually better inverted into an early return plus the function's real logic at the top level.
2. **Nested validation chain.** Two or more consecutive levels of "single `if`, no `else`" nesting (e.g. `if valid: if more_valid: if even_more_valid: ...`), capped at a 4-level scan. This is exactly the shape a chain of guard clauses replaces.
3. **Deep if-nesting.** Three or more levels of nested `if` statements anywhere in the function body (one level below [excessive nesting](excessive-nesting.md)'s general depth-3 threshold, since this detector targets specifically flattenable if-chains).

## Example

```python
def handle(request):
    if request.is_valid():
        if request.user.is_active():
            if request.user.has_permission():
                return process(request)
    return None
```

Flagged as a nested validation chain (three consecutive guard-shaped `if`s, no `else`); the idiomatic fix is:

```python
def handle(request):
    if not request.is_valid():
        return None
    if not request.user.is_active():
        return None
    if not request.user.has_permission():
        return None
    return process(request)
```

## Known limitations

Runs for Python, TypeScript, Kotlin, and C++. F#'s grammar has no block-boundary node to anchor this heuristic on. In C++, only explicit syntax is considered; macro-expanded guard chains are not visible.
