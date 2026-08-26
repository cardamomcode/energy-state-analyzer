# Energy State Analyzer

Visualizes "energy states" in Python, F#, and TypeScript code as you edit: parts of a file that are complex, deeply nested, or otherwise harder to understand and maintain get highlighted with colored gutter icons, inline decorations, and entries in the Problems panel.

## Features

Real-time analysis of the active Python, F#, or TypeScript file, re-run on every edit and on editor focus change, via these detectors (see [docs/detectors](docs/detectors/README.md) for full detail on each):

- [Cyclomatic complexity](docs/detectors/cyclomatic-complexity.md), too many independent execution paths.
- [Cognitive complexity](docs/detectors/cognitive-complexity.md), too hard to read due to nesting.
- [Excessive nesting](docs/detectors/excessive-nesting.md), control-flow blocks nested too deep.
- [File coherence](docs/detectors/file-coherence.md), files that have lost a single responsibility.
- [Magic numbers](docs/detectors/magic-numbers.md), unnamed numeric literals.
- [Magic strings](docs/detectors/magic-strings.md), unnamed string literals at decision points.
- [Parameter explosion](docs/detectors/parameter-explosion.md), functions with too many parameters.
- [Inversion opportunities](docs/detectors/inversion-opportunities.md), nested conditionals that could be guard clauses.
- [Primitive obsession](docs/detectors/primitive-obsession.md), strings/numbers standing in for a real type.
- [Match opportunities](docs/detectors/match-opportunities.md), if/elif chains that could be a match/switch.
- [Logical operator as control flow](docs/detectors/logical-operator-control-flow.md), an `if` hidden behind `&&`/`||`.
- [Opaque boolean literal](docs/detectors/opaque-boolean-literal.md), an unlabeled `true`/`false` at a call site.

Violations are shown three ways:

- A colored background + gutter lightning-bolt icon on the affected lines (orange = high severity, gold = medium, green = low; colors are configurable, see Extension Settings).
- A hover tooltip explaining the specific violation.
- An entry in the Problems panel, sourced as "Energy State Analyzer".

For functions flagged as too complex (cyclomatic or cognitive), a progressive heatmap in the configured high-energy color (orange by default) is also painted across the function body: each contributing line (an `if`, `for`, `and`, etc.) is shaded from light to dark based on how much it drives up that function's complexity relative to its own worst line, so you can see exactly which branches to break apart first, instead of just knowing the function as a whole is complex.

## Energy and Entropy

The name is a deliberate analogy to thermodynamics: a function's "energy" is its complexity, nesting, and parameter count, while its "entropy" is how many ways a reader can misunderstand it or a change can silently break it. See [docs/energy-and-entropy.md](docs/energy-and-entropy.md) for the full explanation of why cyclomatic and cognitive complexity are tracked as separate metrics rather than one score.

## Command-Line Usage

The same detectors also run headlessly, without VS Code, useful for CI or for an AI coding agent that wants to check the complexity of code it just generated and keep refactoring until it's clean:

```bash
npx energy-state-analyzer path/to/file.py   # or .fs / .fsx / .ts
```

See [docs/cli.md](docs/cli.md) for scanning a whole repo, aggregated markdown/JSON/human reports, and diffing a PR against a base branch.

## Requirements

The extension activates automatically when you open a Python, F#, or TypeScript file; it bundles its own grammars for parsing (via `web-tree-sitter`), so no external tools are required. F# files only get a `fsharp` language ID (and so trigger analysis) if you have an F# language extension installed (e.g. [Ionide](https://ionide.io/)), VS Code otherwise treats `.fs` files as plain text.

## Extension Settings

Detector thresholds are configurable under **Settings → Energy State Analyzer**. See each detector's doc (linked under Features above) for what a setting does; the keys and defaults are:

- `energyStateAnalyzer.cyclomaticComplexity.mediumThreshold` / `.highThreshold` (`10` / `15`)
- `energyStateAnalyzer.cognitiveComplexity.mediumThreshold` / `.highThreshold` (`15` / `25`)
- `energyStateAnalyzer.coherence.largeFunctionLines` (`20`)
- `energyStateAnalyzer.coherence.maxLargeFunctions` (`5`)
- `energyStateAnalyzer.coherence.singleDomainNameShare` (`0.7`)
- `energyStateAnalyzer.matchOpportunity.minBranches` (`3`)
- `energyStateAnalyzer.magicNumber.enabled` (`true`)
- `energyStateAnalyzer.magicNumber.allowlist` (`[0, 1, -1, 2]`)
- `energyStateAnalyzer.magicString.enabled` (`true`)
- `energyStateAnalyzer.magicString.minDuplicates` (`2`)
- `energyStateAnalyzer.magicString.allowlist` (`["", "utf-8", "__main__"]`)
- `energyStateAnalyzer.colors.highEnergy` / `.mediumEnergy` / `.lowEnergy` (`#fb8500` / `#ffb703` / `#99dd99`)
- `energyStateAnalyzer.colors.backgroundOpacity` (`0.1`)

Changes take effect immediately on the active editor.

To exclude files/folders (e.g. test fixtures, generated code) from both the extension's live analysis and the CLI, add a `.esaignore` file to your workspace root — see [`docs/cli.md`](docs/cli.md#excluding-files-and-folders-esaignore).

## Commands

- **Energy State Analyzer: Analyze Energy State** (`energy-state-analyzer.analyze`), manually re-run analysis on the active editor.

## Known Issues

- Nesting depth and parameter count thresholds are not yet configurable via VS Code settings, only cyclomatic complexity, cognitive complexity, the large-function coherence check, the match-opportunity branch count, and the magic-number/magic-string detectors are.
- TypeScript arrow functions aren't analyzed by complexity/parameter-count/coherence (same limitation Python already has for `lambda`), only named `function` declarations and class methods are.
- Several detectors have per-language gaps beyond the above, see the "Known limitations" section of the relevant [detector doc](docs/detectors/README.md).
