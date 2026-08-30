# Command-Line Usage

The same detectors also run headlessly, without VS Code, useful for CI or for an AI coding agent that wants to check the complexity of code it just generated and keep refactoring until it's clean. Published to npm, so no clone or install step is required:

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

## Scanning a repo or subtree

Pass more than one path, a directory, or a `dir/**/*.ext`-style pattern to scan every supported file underneath it (skipping `node_modules`, `.git`, `dist`, `out`, `build`, `.next`, `coverage`, `.vscode-test`) and get an aggregated report instead of a single file's violations:

```bash
npx energy-state-analyzer src --report md
```

```
# Energy State Report

**3 files scanned** — 2 clean, 1 with violations

| File | Score | High | Medium | Low |
| --- | --- | --- | --- | --- |
| src/foo.py | 13 | 1 | 1 | 0 |
| src/bar.ts | 0  | 0 | 0 | 0 |
| src/baz.fs | 0  | 0 | 0 | 0 |

**Total score: 13** (1 high, 1 medium, 0 low)
```

`--report json` prints the same data as a structured `{ files, totalScore, totalCounts }` object instead. The per-file **score** is a simple heuristic, `1×low + 4×medium + 9×high` violation counts, meant for spotting hotspots and tracking direction over time, not a certified complexity metric.

Only one glob shape is supported: a trailing `**/*.ext` pattern on an otherwise literal directory prefix (e.g. `src/**/*.py`). There's no brace expansion, negation, or mid-path wildcards, pass explicit directories/files for anything more complex.

### Excluding files and folders: `.esaignore`

Add a `.esaignore` file next to where you run the CLI (or the extension's workspace root) to exclude paths from both `--report`/scan mode and `--base-ref` diff mode. One pattern per line:

```
# comment
src/test/fixtures     # a literal path — matches it and everything under it
generated             # a bare name with no '/' matches at any depth
*.generated.ts        # a basename glob
```

This isn't a full `.gitignore` engine: no negation, no `**`, no brace expansion — just literal path/prefix matches and single-segment basename globs. A richer, TOML-based config file (`.esaconfig.toml`) covering ignore patterns plus other project-wide settings is a likely follow-up.

### A report for humans: `--report human`

`--report md`/`--report json` are compact, built for scripts and PR comments. `--report human` produces a longer, prose-and-tables report meant to be read by a person auditing a repo or subtree: a section per flagged file, each with its findings translated into plain language, followed by a repo-wide "Total evaluation":

```bash
npx energy-state-analyzer src --report human
```

```
# Energy State Report

## Score legend

| Score | Risk | Roughly | Cyclomatic/cognitive complexity |
| --- | --- | --- | --- |
| 0.0 | None | No violations found | — |
| 0.1–3.9 | Low | Simple, easy to test exhaustively | 1–10 |
| 4.0–6.9 | Medium | Getting harder to cover with tests | 11–20 |
| 7.0–8.9 | High | Complex, testing all paths is impractical | 21–50 |
| 9.0–10.0 | Critical | Effectively untestable | 50+ |

**25 files scanned** — 8 clean, 17 flagged

## src/foo.py — High (score 7.8)

- **Cyclomatic complexity**: 1 function scores 34 — score 7.8 (High): complex, testing all paths is impractical.
- **Primitive obsession**: 2 findings (2 medium) — adjacent same-typed values a caller could silently swap without the compiler noticing.

...

## Total evaluation

**Repo score: 7.8 (High)** — driven by the worst file in the scan, `src/foo.py` (complex, testing all paths is impractical).

| Risk | Files |
| --- | --- |
| None | 8 |
| Low | 12 |
| Medium | 3 |
| High | 2 |
| Critical | 0 |

**51 total findings** (1 high, 25 medium, 25 low) — breadth of issues across the scan, independent of peak severity.
```

Risk is reported on a 0.0–10.0 complexity score, sorted into the same None/Low/Medium/High/Critical levels used elsewhere in this tool, rather than a bespoke label set. The score is a direct re-expression of the McCabe risk table (see [Interpreting the score](detectors/cyclomatic-complexity.md#interpreting-the-score)): cyclomatic/cognitive complexity numbers are converted onto it by linear interpolation anchored at the same 10/20/50 breakpoints, so "High" here means the same thing it always has in this project, just expressed as a single number. Every other detector reports a finding count and severity instead, since it flags a pattern rather than a path count, a file with only non-complexity findings gets a fixed score from its worst one (Low 2.0 / Medium 5.0 / High 7.5), which can never reach Critical (Critical is reserved for genuinely extreme complexity).

Both the per-file score and the repo-wide "Repo score" are the **maximum** found, not an average. Averaging a file's (or a repo's) scores lets one severely complex function or file hide behind many trivial ones, nine functions at complexity 2 and one at 60 average to about 8 (which itself would still misleadingly read as "Low"), hiding exactly the function most worth fixing. Total finding counts are reported separately as a breadth indicator, deliberately not folded into the same number. Flagged files are listed worst-first.

## Diffing a PR against a base branch

`--base-ref <ref>` compares the current working tree against a git ref, so a GitHub Actions job can report whether a PR increased or decreased complexity relative to its base branch:

```bash
npx energy-state-analyzer --base-ref origin/main --report md
```

With no path arguments, changed files are discovered via `git diff --name-only <ref>...HEAD`; pass explicit paths to override that. Each changed file's pre-PR content is read with `git show <ref>:<path>` and re-analyzed in memory, a file that doesn't exist at the base ref (new file, or a rename `git diff` didn't resolve) is reported as `new` rather than erroring out.

```
# Energy State Diff vs `origin/main`

| File | Base | Head | Δ | Status |
| --- | --- | --- | --- | --- |
| src/foo.py | 4 | 13 | +9 | 🔴 worsened |
| src/bar.ts | 9 | 0  | -9 | 🟢 improved |
| src/new.py | — | 5  | — | 🆕 new |

_2 files changed, 1 worsened, 1 improved, 1 new._
```

Single-file and scan modes exit `1` for any medium/high-severity violation (`0` otherwise).
Diff mode exits `1` only when a changed file worsens relative to its base revision, so pre-existing
debt and new files are reported without blocking the PR. `energy-state-cli <single-file>` with no
other flags keeps its original flat JSON violation-array contract.
