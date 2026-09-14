# Detectors

Each detector is documented in its own file: what it flags, an example, its configuration, and known limitations. The `ESA###` rule IDs below are stable public identifiers used by SARIF reports and VS Code Problems.

All thirteen detector categories run for C++. Coverage includes functions and methods, typed
declarators, classes and structs, inheritance, includes, literals and constants, boolean operators,
subscripts, `if`/loops/`try`/`catch`, ternaries, and branch-aware `switch` statements. Analysis is
syntax-only: preprocessing, include resolution, template instantiation, overload resolution, and
type checking remain the compiler's job.

## Complexity and structure

- [ESA002: Cyclomatic complexity](cyclomatic-complexity.md), too many independent execution paths.
- [ESA003: Cognitive complexity](cognitive-complexity.md), too hard to read due to nesting.
- [ESA001: Excessive nesting](excessive-nesting.md), control-flow blocks nested too deep.
- [ESA007: Parameter explosion](parameter-explosion.md), functions with too many parameters.
- [ESA005: File coherence](file-coherence.md), files that have lost a single responsibility.
- [ESA005: God class](god-class.md), one type whose methods span too many unrelated domains (the class-level half of file coherence).
- [ESA013: Error shadowing](error-shadowing.md), error handling that overwhelms a function's happy path.

## Naming and literals

- [ESA006: Magic values](magic-values.md), unnamed numeric literals or strings at decision points.
  - [Magic numbers](magic-numbers.md), unnamed numeric literals.
  - [Magic strings](magic-strings.md), unnamed string literals at decision points.
- [ESA009: Primitive obsession](primitive-obsession.md), strings/numbers standing in for a real type.

## Control-flow shape

- [ESA008: Inversion opportunities](inversion-opportunities.md), nested conditionals that could be guard clauses.
- [ESA010: Match opportunities](match-opportunities.md), if/elif chains that could be a match/switch.
- [ESA011: Logical operator as control flow](logical-operator-control-flow.md), an `if` hidden behind `&&`/`||`.
- [ESA015: Parse, don't validate](parse-dont-validate.md), checks whose successful return loses the checked property.
- [ESA012: Opaque boolean literal](opaque-boolean-literal.md), an unlabeled `true`/`false` at a call site.

## Suppression

- [ESA014: Suppression (`esa-ignore`)](suppression.md), silence a specific violation with a comment, without disabling the detector everywhere else.

See [Energy and Entropy](../energy-and-entropy.md) for the design philosophy behind why complexity and its arrangement are tracked as separate signals.
