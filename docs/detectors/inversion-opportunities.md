# Inversion Opportunities

Identifies terminal conditionals that may benefit from guard clauses and deep conditional nesting that may benefit from extracting a named operation. It does not infer that a condition is validation or prove that a refactoring preserves behavior.

## What it flags

Checked per function, with at most one ESA008 finding. A guard-chain recommendation takes precedence over a dominant-block recommendation, then general nesting advice:

1. **Terminal guard chain.** Two or more consecutive, else-free `if` levels at the start of a function. Each outer body contains only the next `if`; the function may end immediately after the outer conditional or have one explicit fallback `return`. Ordinary statements between levels stop the chain, and any other following work prevents guard advice. The message reports the full chain length.
2. **Dominant terminal if-block.** An else-free first `if` whose body contains more than two executable statements and spans more than half the function's source length, with the same terminal-position requirement. The message suggests bringing the main operation to the top level while preserving return values and fallthrough behavior.
3. **Deep if-nesting.** Three or more conditional levels, counting the outermost `if` as level one. `elif` and `else if` alternatives stay at the same level; an `if` inside an explicit `else` body adds a level. This message suggests extracting a named operation, without assuming early returns are appropriate. Nested functions and closures do not contribute to the enclosing function's depth.

All findings have medium severity. These are syntax heuristics, not a control-flow or type analysis. Comments do not count as executable statements; source length remains sensitive to formatting and comments.

## Example

```python
def handle(request):
    if request.is_valid():
        if request.user.is_active():
            if request.user.has_permission():
                return process(request)
    return None
```

This produces one recommendation for three nested conditions. Each failed check already reaches the fallback `return None`, so the guard-clause rewrite preserves that result:

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

An optional operation followed by required work is different:

```python
def handle(request):
    if request.is_valid():
        if request.user.is_active():
            process(request)
    record_attempt(request)
```

This receives no guard recommendation: an early return could skip `record_attempt`. Preserve condition evaluation order, side effects, scope, return values, and any fallthrough work when applying the advice.

## Known limitations

Runs for Python, TypeScript, Kotlin, C++, and C#. F#'s grammar has no block-boundary node to anchor this heuristic on. Guard-chain recognition follows explicit block bodies, so unbraced chains may be missed. Dominant-block recognition requires an explicit block; general depth detection also handles unbraced conditionals. In C++, only explicit syntax is considered; macro-expanded guard chains are not visible.
