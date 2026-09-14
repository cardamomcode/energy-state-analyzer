# Energy and Entropy

Energy State Analyzer uses energy and entropy as an engineering model for the work of
understanding and changing software. The model asks three questions: which possibilities
must a maintainer distinguish, what knowledge do they need to distinguish them, and how
can relationships in the design make a change have unintended consequences?

## Possibilities, relationships, and stored work

In this model, **entropy** concerns the possibilities that remain open given the information
available to a maintainer. **Structural complexity** concerns how parts constrain and affect
each other. **Energy** describes the stored work in a design: interactions and implicit
contracts that a future change may force us to understand and coordinate.

These are related signals, not interchangeable quantities on a common scale, and source
code has no thermodynamic unit. The analyzer surfaces selected signs of that work through
branching, nesting, broad signatures, implicit meanings, and dependency breadth. It does
not calculate the entropy or stored work of a codebase.

A short deployment function can still coordinate provisioning, publication, and health
checks. If publication succeeds but the health check times out, a retry must account for
partial completion, resource ownership, and which operations are safe to repeat. That work
comes from the operations' relationships, even when the function has few branches.

## What the metrics reveal

[Cyclomatic complexity](detectors/cyclomatic-complexity.md) measures independent paths
through a control-flow graph. It describes one aspect of testing effort, rather than every
possible execution or a complete test count.

[Cognitive complexity](detectors/cognitive-complexity.md) estimates reading effort through
breaks in linear control flow and nesting. It does not directly measure cognition. Two
functions can have similar cyclomatic complexity but different cognitive complexity:
nested decisions require the reader to track their enclosing conditions.

The metrics answer different questions, so the analyzer reports them separately. Neither
is an entropy measurement. Flattening control flow can reduce nesting while leaving the
necessary decisions intact; its benefit depends on preserving behavior and making the
operation easier to follow.

## The call space and its constraints

Possibilities also enter through a function's signature. Three independent Boolean
parameters admit eight combinations, even when the domain permits only three. Two string
parameters representing an order ID and a user ID allow a positional swap that their types
cannot distinguish. The caller must supply the missing knowledge about valid combinations,
meaning, and position.

Distinct domain types can preserve those distinctions. Validated constructors can restrict
which values are admitted, while a representation such as a non-empty list can make an
invalid state unrepresentable. A type alias or an unrestricted wrapper alone does not
validate a value. The [primitive-obsession](detectors/primitive-obsession.md) and
[parse-don't-validate](detectors/parse-dont-validate.md) detectors identify selected syntax
patterns where a stronger contract may help.

## The maintainer supplies the decoder

The same code can be straightforward to its author and difficult for someone who lacks
its domain knowledge, conventions, or rationale. That knowledge acts as a **decoder**:
it connects the representation to the problem the code is supposed to solve. More
interpretations remain plausible when less of that knowledge is available.

Types, names, contracts, and decision comments can preserve some of this knowledge close
to the code. A comment explaining why retries reuse an event ID can clarify a deduplication
operation without changing its complexity score. An abstraction helps when it preserves
the distinctions its reader needs; hiding those distinctions can make a shorter program
harder to understand.

The analyzer detects selected places where meaning remains implicit. It cannot determine
everything a reader knows or needs to know, so its findings cover only part of this model.

## Use findings to review the design

A useful refactoring removes illegal or unnecessary possibilities, preserves domain
knowledge, or keeps change local. Extracting a function, grouping parameters, or splitting
a file helps when the resulting boundary represents a coherent responsibility. Forcing
similar-looking code into one abstraction can hide different contracts and make independent
behavior change together, even if a local metric falls.

Some complexity serves the work: exploration, performance-sensitive code, and integration
boundaries may need it. Review whether that complexity is necessary and contained, and
preserve the reason for a deliberate exception.

Severity expresses the analyzer's review priority under the configured rules. Scores help
locate findings and compare runs; a lower score does not establish a better design, and
no findings does not establish code quality. Review the code's purpose and contracts,
verify behavior, and reassess the findings after a change.
