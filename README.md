# Energy State Analyzer

Visualizes "energy states" in Python, F#, and TypeScript code as you edit: parts of a file that are complex, deeply nested, or otherwise harder to understand and maintain get highlighted with colored gutter icons, inline decorations, and entries in the Problems panel.

## Features

- **Real-time analysis** of the active Python, F#, or TypeScript file, re-run on every edit and on editor focus change.
- **Cyclomatic complexity** — flags functions with too many independent execution paths (`if`/`for`/`while`/`except`/boolean operators/ternaries all count equally, regardless of nesting).
- **Cognitive complexity** — flags functions that are hard to *read*, weighting each decision point by how deeply it's nested and not penalizing early-return guard clauses.
- **Excessive nesting** — flags `if`/`for`/`while`/`with` blocks nested more than 3 levels deep.
- **File coherence** — flags files with too many functions or imports (a sign of "utils/helpers sprawl"), and separately flags files with too many large functions (regardless of total function count, so languages like F# with many small functions per module aren't penalized). A file where most functions share a leading name word (e.g. all `extractFoo`/`extractBar`) is treated as one coherent domain and exempted from the function-count check.
- **Magic numbers** — flags numeric literals used outside of a named binding, an index/key position, or a default parameter value. Numbers get no free pass for "looking like prose" the way strings do, so this stays broad; an `energyStateAnalyzer.magicNumber.allowlist` setting (default `[0, 1, -1, 2]`) exempts the values that recur constantly without carrying hidden meaning.
- **Magic strings** — flags a string literal only where an unnamed one actually risks a silent typo: compared with `==`/`===`, checked for membership (Python's `x in (...)`), or used as a dict/object key. A message being logged, thrown, or returned isn't a decision point, so it's left alone entirely — as is any f-string/template-literal/`.format()`/`%`-formatted string, since a placeholder is itself evidence the string isn't a stand-in for an enum value. To cut single-use false positives further, a qualifying literal is only flagged once it recurs at a decision point at least `energyStateAnalyzer.magicString.minDuplicates` times (default `2`) across the file.
- **Parameter explosion** — flags functions with more than 5 parameters.
- **Inversion opportunities** — flags large dominant `if` blocks and nested validation chains that could be rewritten as guard clauses with early returns.
- **Primitive obsession** — flags consecutive same-typed primitive parameters (e.g. `lat: float, lon: float`) that callers can silently swap, and variables compared against 3+ distinct string literals (a de facto enum encoded as strings). Runs on Python, F#, and TypeScript; Python additionally flags a variable checked against a literal tuple/list/set in one `in` expression, since F# and TypeScript have no direct equivalent construct. In Python, a same-typed pair is not flagged when both parameters are keyword-only (after a bare `*` or `*args` in the signature) since a caller can no longer pass them positionally — named-parameter naming is still a weaker mitigation than a distinct type (`NewType`, a dataclass, etc.), since nothing stops a future `**kwargs`-splat call from transposing the values. This suppression doesn't apply to TypeScript (no argument-labeling syntax for positional params) or F# (named arguments are optional at the call site, so they don't prevent a positional call).
- **Match opportunities** — flags an `if`/`elif`/`elif` chain (or TypeScript's nested `else if`) of 3 or more branches that all compare the same single variable to a literal, suggesting a `match`/`switch` statement instead. Runs on Python, F#, and TypeScript.
- **Logical operator as control flow** — flags a bare `condition && doSomething()` (or `condition || fallback()`) statement, an `if` hidden behind a boolean operator instead of written as one. Runs on Python and TypeScript; not on F#, which has no such statement-level idiom in its grammar.
- **Opaque boolean literal** — flags a bare `true`/`false` passed positionally into a call (e.g. `configure(true)`), since a reader can't tell what it means without checking the callee's signature. Suppressed when the boolean is labeled at the call site: a Python keyword argument (`configure(retries=True)`), a TypeScript object-literal field (`configure({ retries: true })`), or F#'s named-argument syntax (`configure(retries = true)`) — unlike the primitive-obsession suppression above, F#'s named args count here even though they're optional, since this rule is about reader comprehension at this call site, not about preventing a future misuse. The preferred fix is usually splitting into two clearly named functions (`enableRetries()`/`disableRetries()`) or an enum; naming the argument is an acceptable but weaker mitigation. Deliberately conservative: only literal `true`/`false` are flagged, not bare `0`/`1`, to avoid noise on ordinary numeric arguments.

Violations are shown three ways:

- A colored background + gutter lightning-bolt icon on the affected lines (orange = high severity, gold = medium, green = low; colors are configurable, see Extension Settings).
- A hover tooltip explaining the specific violation.
- An entry in the Problems panel, sourced as "Energy State Analyzer".

For functions flagged as too complex (cyclomatic or cognitive), a progressive heatmap in the configured high-energy color (orange by default) is also painted across the function body: each contributing line (an `if`, `for`, `and`, etc.) is shaded from light to dark based on how much it drives up that function's complexity relative to its own worst line — so you can see exactly which branches to break apart first, instead of just knowing the function as a whole is complex.

## Energy and Entropy

The name is a deliberate analogy to thermodynamics, not just a metaphor for "bad code."

In physics, energy constrains which microstates a system can occupy, and entropy counts how many of those microstates are compatible with what we observe: `S(E) = k_B ln Ω(E)`. Adding energy usually increases entropy, because there are more ways to distribute it, but *how* it's distributed matters just as much as how much there is. A hot object next to a cold one has lower entropy than the same total energy spread evenly across both, which is why heat spontaneously flows from hot to cold: the system moves toward the macrostate with more compatible microstates.

Code behaves the same way. A function's "energy" here is its cyclomatic/cognitive complexity, nesting depth, parameter count, and so on: the raw amount of decision-making and structure packed into it. Its "entropy" is the number of ways a reader can misunderstand it, the number of code paths a change can silently break, and the number of mental states a maintainer has to hold at once to reason about it correctly. Just as in physics, higher energy tends to raise entropy: a function with more branches and deeper nesting generally has more ways to go wrong. But it's not purely amount, *how* that complexity is arranged matters too:

- A long function with 20 sequential, flat `if`s is high cyclomatic complexity but comparatively low entropy: each branch is independent and easy to reason about in isolation (the "evenly spread" case).
- The same 20 decision points nested five deep inside each other is high *cognitive* complexity: the reader must hold all five levels in mind simultaneously, which is a much higher-entropy (harder to predict, easier to break) arrangement of the same energy.

This is why the extension tracks cyclomatic and cognitive complexity as separate metrics rather than one score: they capture the *energy* and its *arrangement* respectively. Guard clauses, extracted functions, and early returns don't necessarily remove energy from a codebase; they redistribute it into a lower-entropy arrangement, the code equivalent of letting a hot and cold object equilibrate: same total energy, fewer surprising configurations, easier to hold a correct mental model of.

Entropy here also depends on the observer, not just the code. A function's energy is fixed by what's written, but its entropy, the number of arrangements consistent with what someone currently knows, can grow over time even if the code never changes: the original author forgets the reasoning, or a new developer inherits the file with no context. This detector only measures the static, code-side half of that (the energy and its arrangement); the knowledge-decay half is a reason to keep energy low in the first place, since low-entropy code is cheaper to relearn from scratch.

## Cyclomatic Complexity

Counts the number of independent paths through a function. Starting from a base of **1**, every decision point adds **+1**, regardless of how deeply it's nested:

- `if` / `elif` / `while` / `for` / `except`
- `and` / `or`
- ternary (`a if cond else b`)

Two functions with the same number of `if`s score the same, whether those `if`s are sequential or nested five deep — it measures *how many paths exist*, not how hard the code is to follow.

## Cognitive Complexity

Modeled on [SonarSource's metric](https://www.sonarsource.com/resources/cognitive-complexity/): it measures how hard a function is to *read*, so nesting is penalized and straight-line control flow isn't.

- Each decision point (`if`, `elif`, `for`, `while`, `except`, ternary, nested `def`/`lambda`) adds **1 + current nesting depth**.
- `else` adds a flat **+1** — no nesting penalty, since it doesn't add a new branch to reason about.
- Nesting depth only increases when descending into a block body, so an `if` inside two other `if`s scores higher than three sequential `if`s at the top level, even though both have the same cyclomatic complexity.
- Chained boolean operators of the same kind (`a and b and c`) count as a **single** increment rather than one per operator; mixing `and`/`or` starts a new increment.

This project's implementation is a simplified first pass on the SonarSource spec: `for`/`while` `else` clauses are scored like `if`/`else`, boolean-chain merging only looks at the immediate parent operator, and recursive calls aren't specially detected.

## Command-Line Usage

The same detectors also run headlessly, without VS Code — useful for CI or for an AI coding agent that wants to check the complexity of code it just generated and keep refactoring until it's clean. Published to npm, so no clone or install step is required:

```bash
npx energy-state-analyzer path/to/file.py   # or .fs / .fsx / .ts
```

Or install it as a project/global dependency and call it directly:

```bash
npm install --save-dev energy-state-analyzer
npx energy-state-analyzer path/to/file.py
```

It prints violations as JSON to stdout and exits `1` if any medium/high-severity violation was found (`0` otherwise), so it can gate a loop:

```bash
npx energy-state-analyzer path/to/file.py \
  --medium-cyclomatic 8 --high-cyclomatic 12 \
  --medium-cognitive 12 --high-cognitive 20
```

All threshold flags are optional: `--medium-nesting`, `--high-nesting`, `--medium-cyclomatic`, `--high-cyclomatic`, `--medium-cognitive`, `--high-cognitive`.

## Requirements

The extension activates automatically when you open a Python, F#, or TypeScript file; it bundles its own grammars for parsing (via `web-tree-sitter`), so no external tools are required. F# files only get a `fsharp` language ID (and so trigger analysis) if you have an F# language extension installed (e.g. [Ionide](https://ionide.io/)) — VS Code otherwise treats `.fs` files as plain text.

## Extension Settings

Detector thresholds are configurable under **Settings → Energy State Analyzer**:

- `energyStateAnalyzer.cyclomaticComplexity.mediumThreshold` / `.highThreshold`
- `energyStateAnalyzer.cognitiveComplexity.mediumThreshold` / `.highThreshold`
- `energyStateAnalyzer.coherence.largeFunctionLines` — line count above which a function counts as "large" (default `20`).
- `energyStateAnalyzer.coherence.maxLargeFunctions` — number of large functions a file can contain before it's flagged (default `5`).
- `energyStateAnalyzer.coherence.singleDomainNameShare` — share (0-1) of a file's functions that must share a leading name word (e.g. `extractFoo`/`extractBar`) to be treated as one coherent domain, skipping the function-count sprawl check (default `0.7`).
- `energyStateAnalyzer.matchOpportunity.minBranches` — number of branches an if/elif chain must have, all keyed on the same variable, before it's flagged as a match/switch opportunity (default `3`).
- `energyStateAnalyzer.magicNumber.enabled` — whether to flag magic numbers (default `true`).
- `energyStateAnalyzer.magicNumber.allowlist` — numeric literals that are never flagged, regardless of context (default `[0, 1, -1, 2]`).
- `energyStateAnalyzer.magicString.enabled` — whether to flag magic strings (default `true`).
- `energyStateAnalyzer.magicString.minDuplicates` — number of times the same string literal must recur at a decision point before it's flagged (default `2`).
- `energyStateAnalyzer.magicString.allowlist` — string literals that are never flagged, regardless of context (default `["", "utf-8", "__main__"]`).
- `energyStateAnalyzer.colors.highEnergy` / `.mediumEnergy` / `.lowEnergy` — hex colors for the high/medium/low severity background tint and gutter icon (defaults `#fb8500` orange, `#ffb703` gold, `#99dd99` green).
- `energyStateAnalyzer.colors.backgroundOpacity` — opacity of the severity background tint (default `0.1`).

Changes take effect immediately on the active editor.

## Commands

- **Energy State Analyzer: Analyze Energy State** (`energy-state-analyzer.analyze`) — manually re-run analysis on the active editor.

## Known Issues

- Nesting depth and parameter count thresholds are not yet configurable — only cyclomatic complexity, cognitive complexity, the large-function coherence check, the match-opportunity branch count, and the magic-number/magic-string detectors are.
- The magic-string detector's decision-point scan (equality/membership/dict-key) and its formatted-string exemption are fully implemented for Python and partially for TypeScript (no `.includes()` membership support yet) and F# (no dict/subscript node, no interpolated-string exemption) — see the `LanguageAdapter` fields in `src/core/language.ts` for exactly what's modeled per language.
- The magic-string detector doesn't (yet) special-case enum-like keyword/default arguments (e.g. `mode="fast"`) as a lower-confidence decision point — only equality, membership, and dict/index-key positions count.
- The inversion-opportunities detector only fires for Python and TypeScript; F#'s grammar has no block-boundary node to anchor that heuristic on (see Architecture).
- TypeScript arrow functions aren't analyzed by complexity/parameter-count/coherence (same limitation Python already has for `lambda`) — only named `function` declarations and class methods are.
- The primitive-obsession detector's `in (a, b, c)`-style membership check only runs on Python; F#'s grammar has no direct equivalent, and TypeScript's idiom (`[...].includes(x)`) is a call expression rather than a comparison node.
