# Configuration

Analyzer behavior is configured at three levels, resolved in this order of increasing precedence:

```text
built-in defaults  <  .esaconfig.json  <  host override
```

The built-in defaults are the single source of truth for every threshold (see `src/Core/Config.fs`). A project's `.esaconfig.json` layers its values on top, and each host wins at its own boundary — VS Code settings in the editor, command-line flags in the CLI. Anything not set in the file falls back to the built-in default; anything set via a host override beats both.

## The config file: `.esaconfig.json`

Add a `.esaconfig.json` file to configure thresholds, ratios, and magic-number/string allowlists for a whole project — shared between the editor's live analysis and the CLI/CI scan, so one file drives both. It carries the **detail** values (thresholds, allowlists, ratios). Toggles (`enabled`, `includeTestFiles`, `includeFixtures`) and colors stay in VS Code settings; see [Extension Settings](../README.md#extension-settings) — they are not read from this file.

### Discovery

The file is found by walking up parent directories from the start location until `.esaconfig.json` is found or the filesystem root is reached, the same ".gitignore" discovery a linter expects, so one file configures every subtree:

- **Extension:** from the workspace root.
- **CLI:** from the current working directory.

A missing file simply keeps the built-in defaults; malformed JSON is swallowed and also falls back to defaults, so a typo never breaks a scan — it just reverts to the safe values.

```bash
npx energy-state-analyzer src --config path/to/.esaconfig.json   # CLI: skip the walk-up, read this exact file
```

`--config <path>` reads that exact path instead of searching upward; a missing or unreadable file yields the defaults so callers can fall back.

### Schema

Every section is optional; an absent key keeps its default. Numeric keys are camelCase and match the VS Code setting names (minus the `energyStateAnalyzer.` prefix).

```jsonc
{
  "nesting": { "mediumThreshold": 3, "highThreshold": 5 },
  "cyclomaticComplexity": { "mediumThreshold": 10, "highThreshold": 15 },
  "cognitiveComplexity": { "mediumThreshold": 15, "highThreshold": 25 },
  "coherence": {
    "largeFunctionLines": 20,
    "maxLargeFunctions": 5,
    "singleDomainNameShare": 0.7,
    "maxTypeDiversityRatio": 0.4,
    "minTypedCoverage": 0.5
  },
  "matchOpportunity": { "minBranches": 3 },
  "errorShadowing": { "threshold": 0.5, "highThreshold": 0.7, "minNamedNodes": 8 },
  "parameterCount": { "mediumThreshold": 5, "highThreshold": 8 },
  "magicNumber": { "allowlist": [1024, 4096] },
  "magicString": { "minDuplicates": 3, "allowlist": ["pending", "wip"] }
}
```

### Keys and defaults

| Section | Key | Default | What it controls |
| --- | --- | --- | --- |
| `nesting` | `mediumThreshold` | `3` | Nesting depth above which control-flow blocks are flagged as medium energy. |
| `nesting` | `highThreshold` | `5` | Nesting depth above which blocks are flagged as high energy instead of medium. |
| `cyclomaticComplexity` | `mediumThreshold` | `10` | Cyclomatic complexity above which a function is flagged as medium energy. |
| `cyclomaticComplexity` | `highThreshold` | `15` | Cyclomatic complexity above which a function is flagged as high energy instead of medium. |
| `cognitiveComplexity` | `mediumThreshold` | `15` | Cognitive complexity above which a function is flagged as medium energy. |
| `cognitiveComplexity` | `highThreshold` | `25` | Cognitive complexity above which a function is flagged as high energy instead of medium. |
| `coherence` | `largeFunctionLines` | `20` | Line count above which a function counts as "large" for file coherence. |
| `coherence` | `maxLargeFunctions` | `5` | How many large functions (see `largeFunctionLines`) a file may contain before it's flagged for coherence. |
| `coherence` | `singleDomainNameShare` | `0.7` | Share of a file's functions sharing a leading name word to treat the file as one coherent domain and skip the function-count sprawl check. |
| `coherence` | `maxTypeDiversityRatio` | `0.4` | Max ratio of distinct parameter/return base types to typed functions, a stronger cohesion signal when type annotations are trustworthy. |
| `coherence` | `minTypedCoverage` | `0.5` | Minimum share of functions with explicit param/return-type annotations before `maxTypeDiversityRatio` is trusted; below it the detector falls back to `singleDomainNameShare`. |
| `matchOpportunity` | `minBranches` | `3` | Branches (if + elif/else-if) keyed on the same variable an chain must have before it's flagged as a match/switch opportunity. |
| `errorShadowing` | `threshold` | `0.5` | Share of a function's named syntax nodes inside error-handling regions at which it is flagged as medium energy. |
| `errorShadowing` | `highThreshold` | `0.7` | Share at which an error-shadowing finding is high energy. |
| `errorShadowing` | `minNamedNodes` | `8` | Minimum named syntax-node count required before the error-handling share is evaluated. |
| `parameterCount` | `mediumThreshold` | `5` | Parameter count above which a function is flagged for parameter explosion as medium energy. |
| `parameterCount` | `highThreshold` | `8` | Parameter count above which a parameter-explosion violation is flagged as high energy instead of medium. |
| `magicNumber` | `allowlist` | `[0, 1, -1, 2]` | Additional numeric literals to exempt alongside the structural values (see below). The `enabled` toggle stays in VS Code settings. |
| `magicString` | `minDuplicates` | `2` | Times the same string literal must recur at a decision point before it's flagged. |
| `magicString` | `allowlist` | `["", "utf-8", "__main__"]` | String literals never flagged as magic strings, regardless of context. The `enabled` toggle stays in VS Code settings. |

The coherence ratios (`singleDomainNameShare`, `maxTypeDiversityRatio`, `minTypedCoverage`) are floats between `0` and `1`; the thresholds are plain integers.

### Allowlists are unioned, not replaced

A project's magic-number or magic-string allowlist is **unioned** with the built-in structural/sentinel values rather than replacing them, so the structural literals stay exempt no matter what a project sets:

- Magic numbers always keep `0`, `1`, `-1`, and `2` (plus whatever the file adds).
- Magic strings always keep `""`, `"utf-8"`, and `"__main__"` (plus whatever the file adds).

This matches how the extension already extends its baseline: a project can add domain-specific literals without accidentally un-exempting the ones the detectors rely on.

## Host overrides

### Extension (VS Code settings)

In the editor, `energyStateAnalyzer.*` VS Code settings sit above `.esaconfig.json`. The same keys apply — e.g. `energyStateAnalyzer.cyclomaticComplexity.mediumThreshold` overrides the file's value for the current workspace. Colors (`energyStateAnalyzer.colors.*`) and the toggles below are only ever read from VS Code, never from the file. Changes take effect immediately on the active editor.

### CLI (command-line flags)

The CLI reads `.esaconfig.json` by default (or `--config <path>`), then applies threshold flags on top of whatever that file set:

```bash
npx energy-state-analyzer src \
  --medium-cyclomatic 8 --high-cyclomatic 12 \
  --medium-cognitive 12 --high-cognitive 20 \
  --medium-nesting 2 --high-nesting 4 \
  --include-test-files
```

Recognized flags: `--medium-nesting`, `--high-nesting`, `--medium-cyclomatic`, `--high-cyclomatic`, `--medium-cognitive`, `--high-cognitive`, `--medium-parameter-count`, `--high-parameter-count`, and `--include-test-files`. Each threshold flag overrides only the value it provides, so a file can set cyclomatic thresholds while a CI run tightens just cognitive. The magic allowlists, `enabled` flags, and `minDuplicates` all come from the merged base (file over defaults) — only `--include-test-files` overrides them.

See [Command-Line Usage](cli.md) for scanning, reports, and diffing against a base branch.
