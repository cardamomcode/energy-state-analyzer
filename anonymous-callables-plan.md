# Anonymous Callable Analysis Plan

## Goal

Analyze anonymous callables as real function boundaries across Python, F#, TypeScript, Kotlin,
C++, and C# without accidentally broadening unrelated detectors or making file-coherence results
noisy.

The completed work should cover:

- cyclomatic complexity;
- cognitive complexity;
- parameter count; and
- the function-related parts of file coherence.

TypeScript function expressions and C# anonymous methods are included with arrow functions and
lambdas so equivalent anonymous-function syntax does not retain the same gap under another name.

## Current state

- [x] Confirm all six supported language adapters and their anonymous-callable grammar nodes.
- [x] Parse representative anonymous callables with every bundled grammar.
- [x] Verify the existing cyclomatic and cognitive scorers can score an anonymous-callable node
  directly.
- [x] Identify the shared `IsFunctionDefinition` predicate as a boundary used by detectors beyond
  the four in scope.
- [x] Implement anonymous-callable reporting for complexity and parameter count.
- [x] Remove or narrow the corresponding documented limitations for the first behavior change.

The existing scorers are not the main obstacle. The important design work is distinguishing a
callable boundary from a named function definition and distinguishing file/class responsibilities
from inline callbacks.

## Intended semantics

### Callable roles

Use one shared callable representation with enough information to distinguish:

1. **Named definition** — an existing named function, method, constructor, or supported local
   function.
2. **Bound anonymous callable** — an anonymous function assigned directly to a module-level name or
   class member, such as `export const parse = value => ...`.
3. **Inline anonymous callable** — a callback or returned closure, such as
   `items.map(value => ...)`.

The exact F# type and field names may change during implementation, but the distinction must stay
explicit rather than being reconstructed independently inside each detector.

### Detector behavior

- **Cyclomatic complexity:** report every anonymous callable independently. Decisions inside a
  nested anonymous callable must not be folded into the enclosing callable's McCabe graph.
- **Cognitive complexity:** report every anonymous callable independently. Preserve the current
  behavior where a nested closure's body also contributes to its enclosing callable at an
  additional nesting level. These are independent scores and must not be summed as a file total.
- **Parameter count:** report every anonymous callable, including inline callbacks, using the same
  configured thresholds as named functions.
- **File coherence:** count only named definitions and anonymous callables bound at a module or
  class boundary. Do not count inline callbacks or function-local callable variables as additional
  file-level responsibilities.
- **Class coherence:** treat a callable assigned directly to a class member as method-like when the
  language uses that idiom. Do not treat callbacks nested inside a method as more methods on the
  class.
- **Other detectors:** preserve their current named-function behavior unless a separate, explicit
  review shows that anonymous callables belong in their scope.

### Syntax-only limitations to retain

- Do not infer types from compiler or type-checker information.
- Kotlin lambdas without an explicit parameter list may use implicit `it`; because syntax alone
  cannot determine the expected function arity, count only parameters explicitly present in the
  syntax tree.
- C++ macro-expanded code remains invisible without preprocessing.
- Anonymous-callable binding names and class roles are recognized only from direct syntax; aliases,
  returned closures, and assignments through intermediate expressions are not resolved.

## Phase 1: Introduce the callable boundary

- [x] Add a grammar-neutral callable role/type in `src/Core/LanguageAdapter.fs`.
- [x] Expose adapter hooks that can classify a node as a callable and return its analysis view.
- [x] Include an anchor, body, role, optional binding name, and parameter nodes in that view.
- [x] Preserve `IsFunctionDefinition` for detectors that intentionally remain named-function-only,
  or replace it with explicitly named predicates whose consumers retain the same behavior.
- [x] Replace the fixed single `NodeTypes.Parameters` assumption for callable analysis with
  adapter-provided parameter nodes.
- [x] Preserve F# mutually recursive `and`-binding decomposition into independent logical heads.
- [x] Add or update Agent Decision Comments explaining why callable role is separate from syntax
  node type and why unrelated detectors are not broadened.
- [x] Add shared test helpers for locating anonymous-callable nodes and checking their source ranges.

### Phase 1 language adapters

- [x] Python: recognize `lambda` and `lambda_parameters`.
- [x] F#: recognize `fun_expression` and reuse `argument_patterns`.
- [x] TypeScript: recognize `arrow_function` and `function_expression`; support parenthesized and
  bare arrow parameters.
- [x] Kotlin: recognize `lambda_literal` and explicit `lambda_parameters` whose children are
  `variable_declaration` nodes.
- [x] C++: recognize `lambda_expression`, excluding the capture list from parameter count.
- [x] C#: recognize `lambda_expression` and `anonymous_method_expression`; support
  `parameter_list` and single `implicit_parameter` forms.
- [x] Confirm every new fixture parses without missing or error nodes.

## Phase 2: Complexity reporting

### Cyclomatic complexity work

- [x] Traverse all callable views as independently reportable roots.
- [x] Stop a callable's graph and hotspot traversal at every nested callable boundary.
- [x] Ensure a nested anonymous callable is reported separately when it crosses a threshold.
- [x] Add a regression proving branches in an inline callback do not inflate the enclosing named
  function's cyclomatic score.
- [x] Add medium/high threshold-boundary scenarios for anonymous callables in all six languages.
- [x] Verify switch/match/when branch counts and boolean operators retain their current scoring.

### Cognitive complexity work

- [x] Traverse all callable views as independently reportable roots.
- [x] Score an anonymous root from nesting zero for its own diagnostic.
- [x] Preserve the current additional nesting applied when that closure is read inside an enclosing
  callable.
- [x] Add a regression proving a nested closure can have an independent score while still
  contributing to its enclosing callable.
- [x] Add exact-score, hotspot-total, and threshold-boundary scenarios for all six languages.
- [x] Reuse the cached fixture/threshold test pattern so the expanded matrix does not reintroduce
  parallel test timeouts.

## Phase 3: Parameter-count reporting

- [x] Count explicit anonymous-callable parameters through the new adapter hook.
- [x] Anchor diagnostics at the anonymous callable or its direct binding, consistently across hosts.
- [x] Cover clean, medium, and high examples for every language.
- [x] Cover TypeScript and C# single-parameter shorthand.
- [x] Cover default, optional, rest, or variadic forms where the existing language adapter already
  counts the equivalent named-function form.
- [x] Confirm captures in C++ and implicit Kotlin `it` do not become declared parameters.
- [x] Ensure F# `let f = fun ...` and inline `fun ...` expressions both work.
- [x] Confirm existing named-function and F# `and`-binding parameter tests remain unchanged.

## Phase 4: File-coherence integration

Implement this in a separate reviewable change after complexity and parameter reporting are stable.

- [ ] Extract a direct binding name for module-level anonymous callables in every language.
- [ ] Classify direct class-member callables separately from module bindings and inline callbacks.
- [ ] Refactor naming cohesion to consume the callable's explicit optional name rather than searching
  each raw function node for a direct `identifier` child.
- [ ] Refactor type cohesion to consume callable parameter views without weakening typed-coverage
  requirements.
- [ ] Include module-level bound callables in free-function count and large-function sprawl.
- [ ] Include direct class-member callables in method-related coherence checks where the language
  treats them as methods.
- [ ] Exclude callbacks, returned closures, and function-local callable variables from file-level
  function count.
- [ ] Exclude callbacks nested inside methods from class method count and god-class scoring.
- [ ] Preserve import, class-relatedness, and god-class results for files without anonymous
  callables.
- [ ] Add positive and negative coherence fixtures that make the bound-versus-inline policy explicit.

## Phase 5: Cross-language fixture parity

- [x] Add realistic anonymous-callable examples under every
  `src/test/fixtures/<language>/` directory.
- [x] Add named clean and finding scenarios to `tests/DetectorFixtureMatrixTests.fs` for cyclomatic,
  cognitive, and parameter-count behavior.
- [ ] Add explicit coherence scenarios for module-bound, class-bound, function-local, and inline
  callables where those forms exist.
- [x] State syntax forms that a language does not support as explicit limitation cases rather than
  silently omitting them.
- [x] Run the complete registered pipeline in fixture tests; do not validate only private detector
  helpers.
- [x] Assert valid source positions and expected severities for every new diagnostic.

## Phase 6: Documentation and user-facing behavior

- [x] Update the TypeScript adapter's existing decision comment that deliberately records the
  current arrow-function limitation.
- [x] Update `README.md` Known Issues.
- [x] Update `docs/agent-integration.md` coverage notes.
- [x] Update `docs/detectors/cyclomatic-complexity.md` with anonymous-callable boundary semantics.
- [x] Update `docs/detectors/cognitive-complexity.md` with standalone reporting and nested-score
  semantics.
- [x] Update `docs/detectors/parameter-explosion.md` with supported anonymous forms and remaining
  syntax-only limitations.
- [ ] Update `docs/detectors/file-coherence.md` with the bound-versus-inline rule.
- [x] Check `docs/detectors/README.md` and other coverage summaries for stale named-function-only
  wording.
- [x] Ensure examples say “anonymous callable” where the behavior covers more than lambdas alone.

## Phase 7: Validation

Run focused checks after each behavioral phase and the full gate before each PR.

- [x] Run `just format`.
- [x] Run `just lint`.
- [x] Run `just md-lint`.
- [x] Run `just build`.
- [x] Run `just test`.
- [x] Run focused CLI analysis against the six new fixture files and inspect every finding.
- [x] Run `just analyze` and triage every result rather than suppressing it for convenience.
- [x] Run `git diff --check`.
- [x] Review staged and unstaged changes separately and stage only files belonging to this work.
- [x] Confirm no unrelated TypeScript fixture modification or untracked skill note was present or
  modified in this worktree.

## Delivery sequence

### Pull request 1: Callable model, complexity, and parameters

- [x] Land the shared callable abstraction and six adapter implementations.
- [x] Land cyclomatic, cognitive, and parameter-count behavior with cross-language fixtures.
- [x] Update the detector documentation affected by this first behavior change.
- [x] Verify CI and review any changes to existing findings caused by isolating nested callable
  boundaries.

### Pull request 2: Bound-callable coherence

- [ ] Branch from updated `main` after pull request 1 lands.
- [ ] Land binding-name/class-role extraction and coherence integration.
- [ ] Add bound-versus-inline coherence fixtures and documentation.
- [ ] Verify CI and dogfood the packaged CLI against representative real repositories before
  removing the final coherence limitation.

## Completion criteria

- [x] Every supported anonymous-function form has an explicit supported or unsupported case.
- [x] Anonymous callables can independently produce cyclomatic, cognitive, and parameter-count
  diagnostics in all six languages.
- [x] Nested anonymous callables do not inflate an enclosing cyclomatic graph.
- [x] Nested anonymous callables preserve the documented enclosing cognitive-nesting behavior.
- [ ] File coherence counts named responsibilities without counting ordinary inline callbacks.
- [x] Unrelated detectors retain their prior scope and fixture results.
- [x] CLI and extension use the same behavior through the shared Core pipeline.
- [x] Documentation no longer claims the resolved limitation and accurately states what remains.
- [ ] Both pull requests pass the repository's formatting, build, test, markdown, analyzer, and diff
  checks.

## Rough estimate

- Callable model, complexity, parameters, fixtures, and documentation: **2–3 development days**.
- Coherence binding/classification, fixtures, dogfooding, and documentation: **2–4 development
  days**.
- Total expected effort: **4–7 development days**, split into two independently reviewable pull
  requests.
