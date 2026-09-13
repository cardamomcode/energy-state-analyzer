# Integrating with an AI coding agent

The analyzer ships as a CLI that runs headlessly, so an AI coding agent (Claude Code, OpenAI
Codex, Cursor, Copilot, etc.) can use it to check the code it just wrote and keep refactoring until
the complexity is clean, or to review a PR before it lands. This document covers the integration
points that matter to agents: machine-readable output, exit codes for gating loops, shared
configuration, and a couple of example workflows.

If you are working *in this repository*, see [`AGENTS.md`](../AGENTS.md) for how the analyzer is
built and run against its own F# source.

## Why an agent needs it

Language-model editors generate code fast and tend to accumulate complexity: deep nesting, long
functions, magic numbers, and `if` chains that could be a match. The analyzer turns those patterns
into concrete, line-numbered findings with remediation guidance, so an agent can close the loop (run after each edit, stop when the exit code is `0`):

```text
write/edit code  →  run analyzer  →  fix flagged functions  →  re-run until clean
```

It flags *readability and maintainability* risks.
See [docs/detectors](detectors/README.md) for what each detector checks.

## The fast path: single file, JSON, exit code

The simplest integration scans one file and prints violations as JSON to stdout, exiting `1` when
any medium/high-severity violation is found (`0` otherwise). That exit code lets an agent gate a
refinement loop without parsing output:

```bash
npx energy-state-analyzer path/to/file.py --report json   # or .fs / .fsx / .ts / .kt / .cpp / .cs
echo $?   # 0 = clean, 1 = blocking violations found
```

Findings live under `files[].violations` in the JSON report. Each finding includes its zero-based `line` and `column`, the violation `type`, its
`severity`, the detector `message` (which contains a concrete suggested fix), and any `hotspots`.
The agent reads the line numbers, edits those functions, and re-runs.

Override the thresholds inline instead of editing a config file:

```bash
npx energy-state-analyzer path/to/file.py --report json \
  --medium-cyclomatic 8 --high-cyclomatic 12 \
  --medium-cognitive 12 --high-cognitive 20
```

All threshold flags are optional: `--medium-nesting`, `--high-nesting`, `--medium-cyclomatic`,
`--high-cyclomatic`, `--medium-cognitive`, `--high-cognitive`. See
[docs/configuration.md](configuration.md) for every key and its default.

## Scanning a repo or subtree

Pass a directory or a single trailing-`**/*.ext` glob to scan everything underneath it (dependency,
generated, and test-output directories such as `node_modules`, `.git`, `bin`, `obj`, `dist`, `out`,
and Fable outputs are skipped automatically):

```bash
npx energy-state-analyzer src --report md   # table report for a PR comment
npx energy-state-analyzer 'src/**/*.ts' --report json
```

`--report json` prints a structured `{ files, totalScore, totalCounts }` object; `--report md`
prints the compact table. Both are built for scripts and PR comments. See
`--report human` in [docs/cli.md](cli.md) when you want prose-and-tables output instead.

## The interoperable path: SARIF

SARIF 2.1.0 is the default scan output, so the analyzer drops into existing agent and code-scanning
tooling without a custom parser. Each result has a stable `ESA-###` rule ID (for example,
`ESA-006` for magic literals), a one-based source location, a severity mapped to SARIF
`error`/`warning`/`note`, and the detector message with its remediation guidance:

```bash
npx energy-state-analyzer src > latest.sarif
```

Agents that consume SARIF (VS Code's SARIF Viewer, GitHub Code Scanning, Semgrep, etc.) get findings
for free. In VS Code, **Energy State Analyzer: Export SARIF Report** writes `.energy-state/latest.sarif`
for the open workspace.

## Sharing thresholds with the agent

Set thresholds and allowlists in an `.esaconfig.json` at your project root so the editor, the CLI,
and every agent run agree on what "too complex" means. The keys are all optional; an absent key
keeps its default:

```json
{
  "cognitiveComplexity": { "mediumThreshold": 12, "highThreshold": 20 },
  "magicNumber": { "allowlist": [0, 1, -1, 2] },
  "matchOpportunity": { "minBranches": 3 }
}
```

See [docs/configuration.md](configuration.md) for the full schema and guidance on choosing values.
Enabling or disabling a detector stays in the editor (VS Code settings), since that is an
editor-only convenience. Thresholds belong in the shared project file.

## Excluding generated code

Agents and build tools emit files the analyzer should never flag. Add a `.esaignore` file next to
where you run the CLI (or at the workspace root) to exclude them from both scan mode and diff mode:

```text
# A literal path matches itself and everything under it.
src/test/fixtures
# A bare name with no '/' matches at any depth.
generated
# A basename glob.
*.generated.ts
```

Comments must occupy their own lines. This is not a full `.gitignore` engine: no negation, no `**`, no brace expansion. See
[docs/cli.md](cli.md#excluding-files-and-folders-esaignore).

## Reviewing a PR before it lands

`--base-ref <ref>` compares the working tree against a git ref and reports whether a change made a
file worse or better, so an agent can summarize the complexity cost of a diff:

```bash
npx energy-state-analyzer --base-ref origin/main --report md
```

With no path arguments, changed files are discovered via `git diff --name-only <ref>...HEAD`. Diff
mode exits `1` only when a changed file *worsens* relative to its base revision, so pre-existing
debt and new files are reported without blocking the PR. See
[docs/cli.md](cli.md#diffing-a-pr-against-a-base-branch).

## Example workflows

### Refinement loop (generic agent)

Use this pseudocode in the agent's orchestration layer:

```text
repeat:
  run npx energy-state-analyzer src/foo.py --report json
  capture stdout, stderr, and the exit code
  if the exit code is 0: stop
  if stdout is not a valid report: surface stderr and stop
  read findings from files[].violations in stdout
  edit the flagged functions and save the file
  if no fix can be made: report the unresolved findings and stop
```

The CLI performs analysis; the agent supplies the editing step. Exit code `1` can also mean an
analysis failure, so inspect the output before attempting another edit.

### Claude Code

Add a hook or a manual step that runs after edits and feeds the JSON back into the edit decision:

```bash
npx energy-state-analyzer src/foo.ts --report json
```

The `message` field already contains the suggested fix (e.g. "extract this branch into a guard
clause"), so the agent can act on it directly. For a whole-repo audit, run scan mode and post the
`md` report as a summary.

### GitHub Actions for PR review

A composite action in this repo runs the analyzer over a path or a PR diff and posts a sticky PR
comment (configurable `path`, `base-ref`, `report-format`, and `fail-on-regression`). Wire it into a
workflow with:

```yaml
- uses: actions/checkout@v6
  with:
    fetch-depth: 0
- uses: cardamomcode/energy-state-analyzer@<tag>
  with:
    path: src
    base-ref: origin/main
    report-format: md
    fail-on-regression: true
```

Replace `<tag>` with a published release tag. The job needs `contents: read` and
`pull-requests: write` permissions to check out the code and post the PR comment.

See [`action.yml`](../action.yml). For code-scanning integration, the `fsharp-analyzers` CI job in
this repo uploads SARIF per project to GitHub Code Scanning, a good template for wiring any
SARIF-consuming analyzer into CI. See [AGENTS.md](../AGENTS.md) (the "F# analyzers" section) for that SARIF-in-CI pattern.

## Local git hooks

The same exit-code contract gates a commit: the CLI exits `1` on any medium/high violation, so a
hook that runs it before `git commit` blocks complex code from being committed at all. Two options,
both run only on staged files:

### pre-commit

Best if you want a reusable, multi-tool gate (this plus formatting/markdown checks) that every agent
gets identically after `pre-commit install`. Add `.pre-commit-config.yaml`:

```yaml
repos:
  - repo: https://github.com/pre-commit/pre-commit-hooks
    rev: v5.0.0
    hooks:
      - id: end-of-file-fixer
      - id: trailing-whitespace
  - repo: local
    hooks:
      - id: energy-state
        name: Energy State Analyzer
        entry: npx energy-state-analyzer
        language: system
        files: '\.(py|fs|fsx|ts|kt|cpp|cs)$'
        pass_filenames: true
```

### husky + lint-staged

Best if you want to stay entirely in npm (this is already an npm package) with no extra framework.
Install the tools and initialize Husky:

```bash
npm install --save-dev husky lint-staged energy-state-analyzer
npx husky init
```

Merge these entries into `package.json`, preserving existing scripts. Husky initialization adds
`prepare` under `scripts`:

```json
{
  "scripts": {
    "prepare": "husky"
  },
  "lint-staged": {
    "*.{py,fs,fsx,ts,kt,cpp,cs}": ["energy-state-analyzer"]
  }
}
```

Replace the generated command in `.husky/pre-commit` with:

```sh
npx lint-staged
```

Either way the hook passes the staged file paths to the CLI; it exits non-zero on a violation and
`git commit` is aborted. The analyzer does not rewrite or suppress findings. A developer (or agent) must fix
the flagged functions, then re-stage and commit.

## Notes on coverage

- Analysis is **syntax-only** via bundled `web-tree-sitter` grammars; no compiler, type checker,
  or preprocessor is invoked. C++ does not preprocess macros or instantiate templates; TypeScript
  arrow functions and Python lambdas are not analyzed by the complexity/parameter/coherence
  detectors (named declarations and methods are). See the "Known limitations" section of each
  [detector doc](detectors/README.md) and the README's Known Issues.
- The per-file **score** (`1×low + 4×medium + 9×high`) is a hotspot-spotting heuristic for tracking
  direction over time, not a certified complexity metric. Severity counts are the authoritative signal.
