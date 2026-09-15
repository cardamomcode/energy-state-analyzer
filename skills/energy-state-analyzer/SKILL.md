---
name: energy-state-analyzer
description: Write or refactor Python, F#, TypeScript, Kotlin, C++, or C# code to avoid Energy State Analyzer findings, then triage and verify the result. Use when a project uses the Energy State Analyzer extension or CLI; this complements rather than replaces tests, type checking, formatting, and project-specific design rules.
---

# Energy State Analyzer

Produce domain-correct code that passes the project's analyzer policy without gaming metrics.

## Before writing

1. Read the repository instructions and nearby code. Preserve behavior, public contracts, side
   effects, evaluation order, and existing design rationale.
2. Find the nearest `.esaconfig.json` by walking upward from the working directory. Its values
   override analyzer defaults; do not weaken them merely to make new code pass.
3. Choose a coherent responsibility for each file and function. Identify domain values and
   invariants that should be represented by types rather than comments or repeated checks.

## Write for a clean first scan

- Keep the happy path visible. Prefer shallow control flow and guard clauses when early exit truly
  preserves fallthrough behavior. Separate independent responsibilities, not arbitrary chunks.
- Keep each function comfortably below the configured nesting, cyclomatic, cognitive, and
  parameter thresholds. Without project overrides, findings begin above depths `3`, cyclomatic
  score `10`, cognitive score `15`, and `5` parameters.
- Use `match`/`switch` for three or more branches dispatching on the same value. Write an explicit
  `if` instead of hiding control flow in a standalone `&&`, `||`, `and`, or `or` expression.
- Group parameters only when they form one named concept. Use distinct domain/value types for
  adjacent same-primitive parameters and enums/unions for finite string states. Avoid generic
  parameter bags.
- Replace positional boolean literals with a named operation, enum, named argument, or labeled
  settings object appropriate to the language.
- When validation establishes a useful property, return or construct a type that preserves it.
  Do not validate a primitive or collection and then return the same unconstrained value unless
  the boundary deliberately requires that contract.
- Name decision-driving numeric values. Extract repeated decision strings or model them as a
  finite state type. Keep obvious structural values and one-off presentation text inline.
- Keep a `try` region limited to operations handled by its adjacent recovery policy. Keep handlers
  and cleanup focused; the default recovery-block limit is `20` nonblank, noncomment lines.
- Keep files about one domain. Avoid growing `utils`, `helpers`, or `common` grab bags; prefer an
  existing cohesive module or a domain-named boundary.

These are design prompts, not targets to optimize mechanically. A helper, wrapper, or new file is
useful only when it creates a real boundary, preserves knowledge, or makes behavior independently
understandable and testable.

## Analyze and finish

Prefer the repository's existing analyzer command. Otherwise run the installed CLI on the changed
supported files:

```bash
npx energy-state-analyzer <changed-files> --report json
```

Then:

1. Confirm stdout is a valid successful report. Exit code `1` can mean blocking findings or an
   analysis failure; inspect stdout and stderr rather than retrying blindly.
2. Read every item in `files[].violations`, including low-severity findings that can accompany
   exit code `0`. Locations in JSON are zero-based; use each `message` and `hotspots` as evidence.
3. Triage each finding as a valid issue, a legitimate exception, or a detector/configuration
   problem. Fix valid issues while preserving behavior, then run focused behavioral checks.
4. Use a typed `esa-ignore` only for a reviewed exception, with a nearby reason and only when
   project policy permits it. Never add a suppression merely to obtain a clean run.
5. Re-run the analyzer on the changed files. Before handoff, run the project's broader analyzer
   command when practical and report any remaining findings with their disposition or blocker.

A clean report means no enabled detector found a covered pattern under the current configuration.
It does not prove correctness or overall code quality.
