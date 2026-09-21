# Integrating with an AI coding agent

The analyzer ships as a CLI that runs headlessly, so an AI coding agent (Claude Code, OpenAI
Codex, Cursor, Copilot, etc.) can use it to fix findings in the code it just wrote, verify the
result, or review a PR before it lands. This document covers the integration points that
matter to agents: machine-readable output, exit codes for gating loops, shared
configuration, and a couple of example workflows.

If you are working *in this repository*, see [`AGENTS.md`](../AGENTS.md) for how the analyzer is
built and run against its own F# source.

## Why an agent needs it

Generated code can contain deep nesting, long functions, magic numbers, and `if` chains that
could be a match. The analyzer turns those patterns into concrete, line-numbered findings
with remediation guidance, so an agent can act on them and verify the result:

```text
write/edit code  →  analyze  →  triage findings  →  fix valid findings  →  verify and re-analyze
```

Review each finding in the context of the code's purpose. A useful refactoring removes
unnecessary possibilities, preserves domain knowledge, or keeps change local. Extracting a
function, grouping parameters, or splitting a file helps when the resulting boundary
represents a coherent responsibility. A lower local score can still leave more scattered
dependencies or a harder-to-understand abstraction.

Fix valid findings using the detector's remediation guidance. For a legitimate exception,
preserve the reason and use a supported suppression if project policy permits it; do not
suppress a finding merely to pass a gate. For a suspected detector error or unsuitable
threshold, provide a concrete example and seek a detector or configuration correction.
Verify behavior and re-analyze after each fix. Continue until every finding is fixed or has
an explicit disposition. If a fix is blocked, report the remaining finding and the blocker;
do not present the run as complete merely because the exit code is `0`.
A successful analysis with no findings establishes only that no enabled rule reported a
pattern under the current configuration and language coverage. It does not establish code
quality. See [Energy and Entropy](energy-and-entropy.md) for the model behind this review.

The analyzer flags *readability and maintainability* risks.
See [docs/detectors](detectors/README.md) for what each detector checks.

## The fast path: single file, JSON, exit code

The simplest integration scans one file and prints violations as JSON to stdout, exiting `1` when
any medium/high-severity violation is found (`0` otherwise). That exit code lets an agent gate a
refinement loop; inspect the report to distinguish findings from analysis failures:

```bash
npx energy-state-analyzer path/to/file.py --report json   # or .fs / .fsx / .ts / .kt / .cpp / .cs
echo $?   # 0 = no blocking findings; 1 = blocking findings or analysis failure
```

Findings live under `files[].violations` in the JSON report. Each finding includes its zero-based `line` and `column`, the violation `type`, its
`severity`, the detector `message` (which contains a concrete suggested fix), and any `hotspots`.
The agent uses the locations and suggested fixes to resolve valid findings, verifies behavior,
and re-runs. Low-severity findings can remain even when the exit code is `0`.

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

`--report json` prints a structured `{ files, filesScanned, totalScore, totalCounts }` object
(`files` lists only files with findings, `filesScanned` is the number of files analyzed); `--report md`
prints the compact table. Both are built for scripts, PR comments, and coding agents — `--report json`
is the default scan output. See
`--report human` in [docs/cli.md](cli.md) when you want prose-and-tables output instead.

## The interoperable path: SARIF

SARIF 2.1.0 is available with `--report sarif`, so the analyzer drops into existing code-scanning
tooling without a custom parser. Coding agents (Claude, Codex, and similar) should use the default
JSON report — the same findings in a flat, compact `files[].violations[]` shape — and reach for
SARIF only when the consumer is a code-scanning service. Each result has a stable `ESA###` rule ID (for example,
`ESA006` for magic literals), a one-based source location, a severity mapped to SARIF
`error`/`warning`/`note`, and the detector message with its remediation guidance:

```bash
npx energy-state-analyzer src > latest.sarif
```

Tooling that consumes SARIF (VS Code's SARIF Viewer, GitHub Code Scanning, Semgrep, etc.) gets findings
for free. In VS Code, **Energy State Analyzer: Export SARIF Report** writes `.energy-state/latest.sarif`
for the open workspace.

## Sharing thresholds with the agent

Set thresholds and allowlists in an `.esaconfig.json` at your project root so the editor, the CLI,
and every agent run agree on which patterns need fixing and their severity. The keys are all optional; an absent key
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

`--base-ref <ref>` compares the working tree against a git ref and reports changes in the
weighted finding score, so an agent can identify files that need review:

```bash
npx energy-state-analyzer --base-ref origin/main --report md
```

With no path arguments, changed files are discovered via `git diff --name-only <ref>...HEAD`. Diff
mode exits `1` for a score regression in a changed file relative to its base revision, so
pre-existing debt and new files are reported without blocking the PR. See
[docs/cli.md](cli.md#diffing-a-pr-against-a-base-branch).

## Example workflows

### Refinement loop (generic agent)

Use this pseudocode in the agent's orchestration layer:

```text
repeat:
  run npx energy-state-analyzer src/foo.py --report json
  capture stdout, stderr, and the exit code
  if stdout is not a valid successful report: surface the failure and stop
  read findings from files[].violations in stdout, including low-severity findings
  triage each finding: valid issue, legitimate exception, or detector/configuration problem
  record reasons for exceptions and concrete examples for detector/configuration problems
  if no valid issues remain: report the dispositions and stop
  if the remaining fixes are blocked: report unresolved findings and blockers and stop
  fix a valid issue using its remediation guidance and verify behavior
  if verification fails: correct or revert the change before continuing
```

The CLI performs analysis; the agent supplies the editing step. Exit code `1` can also mean an
analysis failure, so inspect the output before attempting another edit.

### Claude Code

Add a hook or a manual step that runs after edits and feeds the JSON back into the edit decision:

```bash
npx energy-state-analyzer src/foo.ts --report json
```

The `message` field already contains the suggested fix (e.g. "extract this branch into a guard
clause"), so the agent can apply it while preserving return values, side effects, and scope,
then verify behavior and re-analyze. For a whole-repo audit, run scan mode and post the `md`
report as a summary.

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
hook that runs it before `git commit` blocks commits with those findings or analysis failures.
Two options, both run only on staged files:

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
`git commit` is aborted. The analyzer does not rewrite or suppress findings. A developer (or
agent) must fix valid findings, document legitimate exceptions under project policy, and
resolve analysis failures before re-staging and committing.

## Notes on coverage

- Analysis is **syntax-only** via bundled `web-tree-sitter` grammars; no compiler, type checker,
  or preprocessor is invoked. C++ does not preprocess macros or instantiate templates. Anonymous
  callables are independent cyclomatic, cognitive, and parameter-count boundaries, but file
  coherence still counts only named definitions and methods. See the "Known limitations" section
  of each [detector doc](detectors/README.md) and the README's Known Issues.
- The per-file **score** (`1×low + 4×medium + 9×high`) is a hotspot-spotting heuristic for tracking
  changes in findings over time. Severity communicates the seriousness of the detected
  readability and maintainability risks; individual messages explain what to fix. The score
  and counts cover the enabled rules, not every aspect of code quality.
