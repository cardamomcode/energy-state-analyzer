# Detectors

Each detector is documented in its own file: what it flags, an example, its configuration, and known limitations.

## Complexity and structure

- [Cyclomatic complexity](cyclomatic-complexity.md), too many independent execution paths.
- [Cognitive complexity](cognitive-complexity.md), too hard to read due to nesting.
- [Excessive nesting](excessive-nesting.md), control-flow blocks nested too deep.
- [Parameter explosion](parameter-explosion.md), functions with too many parameters.
- [File coherence](file-coherence.md), files that have lost a single responsibility.

## Naming and literals

- [Magic numbers](magic-numbers.md), unnamed numeric literals.
- [Magic strings](magic-strings.md), unnamed string literals at decision points.
- [Primitive obsession](primitive-obsession.md), strings/numbers standing in for a real type.

## Control-flow shape

- [Inversion opportunities](inversion-opportunities.md), nested conditionals that could be guard clauses.
- [Match opportunities](match-opportunities.md), if/elif chains that could be a match/switch.
- [Logical operator as control flow](logical-operator-control-flow.md), an `if` hidden behind `&&`/`||`.
- [Opaque boolean literal](opaque-boolean-literal.md), an unlabeled `true`/`false` at a call site.

See [Energy and Entropy](../energy-and-entropy.md) for the design philosophy behind why complexity and its arrangement are tracked as separate signals.
