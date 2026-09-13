# Agent Decision Comments

> Preserve the why in agent-written, human-reviewed code.

- Specification version: **0.4.1**
- Full specification: <https://github.com/dbrattli/adc>

Agent Decision Comments (ADCs) are concise, structured annotations that keep
durable engineering decisions beside the code they govern. They use ordinary
comments, documentation comments, or docstrings, and they require no runtime
dependency.

## The four labels

```text
decision:    A deliberate choice and the reason for it.
invariant:   A falsifiable property that the code must preserve.
assumption:  An external belief that this code does not guarantee.
tradeoff:    A cost accepted in exchange for a benefit.
```

Directives are optional. Add only those that capture non-obvious rationale.
Start with `decision:` and `invariant:`. A caller obligation is a precondition,
not an assumption.

## Format

```text
<label>: <statement> [— <reason or consequence>]
```

- One directive per physical line, in present tense and as concise as
  practical.
- State only what is not already evident from code, types, or tests.
- Prefer specific, falsifiable statements over general claims.
- In a docstring or documentation comment, write normal prose first, leave a
  blank line, then list the directives.
- Use an ordinary comment when API documentation is not needed.
- Update or remove a directive whenever its code or rationale changes.

The em dash is a readability convention, not a parser delimiter.

```python
def process_events(queue, handle):
    """
    Deliver events from a deque to a synchronous handler in enqueue order.
    The caller owns the deque exclusively until processing finishes.

    decision: processes events on one consumer to preserve arrival order
    decision: routes events through a queue to decouple producers from handler timing
    invariant: handlers observe events in enqueue order
    tradeoff: limits throughput to gain deterministic processing
    """
    while queue:
        handle(queue.popleft())
```

## When to write one

Write or update an ADC when a change:

- chooses one meaningful algorithm, architecture, or data structure over
  another;
- introduces ordering, consistency, concurrency, or lifecycle constraints;
- establishes a boundary that future code must not accidentally cross;
- relies on an external condition that could prove false;
- accepts a known limitation in return for a concrete benefit;
- implements behavior a competent reviewer would reasonably question;
- distills a relevant RFC or ADR decision at the point where it constrains code.

Do not add one for mechanics that are clear from the implementation, facts
enforced by the type system, trivial mechanical refactoring, generic advice,
speculative reasoning, or every function merely for consistency.

## Scope

A directive governs the construct it is attached to and its nested constructs.
Directives from enclosing scopes accumulate. A narrower directive may
specialize a broader decision but never silently cancels an invariant. Before
modifying code, collect the active directives from the file level down to the
modified site. If active directives conflict, surface the conflict before
changing the code.

## Existing comments are active constraints

Before modifying code, read the ADCs already governing it.

- Preserve an `invariant:` or justify the change explicitly.
- Do not silently reverse a `decision:`.
- Validate an `assumption:` when the change depends on it.
- Reconsider a `tradeoff:` when its cost or benefit changes.

If a decision changes, update or remove the annotation in the same change and
call it out in the review summary. An obsolete directive is worse than no
directive: it creates false confidence for reviewers and future agents.
