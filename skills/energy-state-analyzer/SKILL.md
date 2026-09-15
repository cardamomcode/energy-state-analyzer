---
name: energy-state-analyzer
description: Write or refactor Python, F#, TypeScript, Kotlin, C++, or C# for projects using Energy State Analyzer, then triage and verify its findings.
---

# Energy State Analyzer

Produce domain-correct code that passes project policy without gaming metrics.

Before editing, read repository instructions, nearby code, and the nearest `.esaconfig.json`.
Preserve behavior, contracts, side effects, evaluation order, and design rationale. Project thresholds
are authoritative; do not weaken them to pass.

## Shape the code

- Give each file and function one coherent responsibility. Extract real domain boundaries, not
  arbitrary chunks or generic `utils`.
- Keep the happy path shallow and functions comfortably below configured nesting, complexity, and
  parameter limits. Use guards only when they preserve control flow.
- Use `match`/`switch` for repeated dispatch on one value. Do not disguise statement control flow
  with standalone boolean operators.
- Model finite states and validated values with types. Group parameters only when they form a named
  concept; avoid adjacent same-primitive parameters and positional boolean literals.
- Name decision-driving numbers and repeated decision strings; leave obvious structural and
  presentation literals inline.
- Limit each `try` to work handled by its adjacent recovery policy; keep recovery focused.

## Verify

Prefer the repository wrapper. Otherwise scan only changed supported files:

```bash
npx energy-state-analyzer <changed-files> --report json
```

Parse every `files[].violations` item, including low-severity findings on exit `0`. JSON locations
are zero-based. Exit `1` means blocking findings or analysis failure, so inspect stdout and stderr.

Triage each finding as valid, an intentional exception, or a detector/configuration problem. Use
its message and hotspots to fix valid issues without changing behavior; run focused tests and scan
again. Add a typed `esa-ignore` only for a reviewed, reasoned exception allowed by project policy.
Report unresolved findings. A clean scan covers only enabled detectors, not correctness or overall
code quality.
