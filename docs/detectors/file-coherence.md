# File Coherence

Flags files that have lost a single responsibility, the "utils/helpers sprawl" pattern, along three independent signals.

## What it flags

**Function-count sprawl.** A file with more than 12 functions is flagged (medium; high past 15). The threshold drops to 8 if the filename itself contains `util`, `helper`, or `common`, treating the name as a proxy for "already known to be a grab-bag." The violation message deliberately steers away from "just split this file" — since the detector only sees one file, it can't tell whether a split would land the code in an existing cohesive module or just distribute the same coupling across new files with more imports wiring them back together.

A file is exempted from this check regardless of count if either of two independent cohesion signals says otherwise (unless the filename already admits to being utils/helper/common, which overrides both):

- *Naming cohesion*: most of its functions share a leading name word (e.g. `extractFoo`/`extractBar`/`extractBaz`, at least a 70% share by default) — one coherent domain broken into small steps, not a grab-bag.
- *Type cohesion*: among the file's typed functions (those with at least one typed parameter or return-type annotation), the ratio of distinct base types touched to typed functions is at or below 0.4 by default. This catches a pattern naming cohesion misses entirely: an F#-style module exposing one verb per operation (`map`/`filter`/`fold`/`zip`/`scan`/...) shares no name prefix at all, but nearly every function touches the module's own type family (confirmed against a real example, `expression/collections/seq.py`, whose ~97 functions almost all touch `Iterable`/`Seq`/`Iterator`). The ratio is deliberately about *type reuse*, not one type reaching a majority share — a cohesive module often legitimately spans a family of related types, and requiring one to dominate false-negatived on exactly that case. Type cohesion is checked first (stronger signal, not vulnerable to name-prefix coincidence); the naming heuristic is only consulted when there's too little type-annotation coverage to trust the ratio (fewer than half the file's functions carry any type annotation, by default).

If a file is still going to be flagged and its type signal is confidently measured as *diverse* (real type coverage, no shared type family), that's treated as authoritative over naming — a consistent naming convention (`parse_date`/`parse_json`/`parse_csv`) touching unrelated types is still a real entropy dump — and produces a distinct, stronger message naming how many unrelated types the file spans. This is only ever evaluated at the same 8/12 function-count thresholds as everything else above, not an earlier one: a lower threshold was tried and rejected after it produced a real false positive on this project's own `coherence.ts` (a handful of purpose-cohesive helper functions using a few different supporting types, not a grab-bag) — type diversity alone isn't a reliable enough signal below ~12 functions to tell the two apart.

**Large-function sprawl.** Counted independently of the check above, on the theory that a module with 30 small functions is fine but one with 6 sprawling ones isn't. A file with more than 5 functions exceeding 20 lines is flagged (medium; high past 7.5 large functions). This gates on large-function count rather than raw function count so that languages like F#, which idiomatically have many small functions per module, aren't penalized just for having a lot of them.

**Import sprawl.** A file drawing from more than 10 distinct modules/packages is flagged (medium; high past 15). Counts distinct import *sources*, not raw import lines/symbols, since that's not comparable across languages: TS (`import { a, b, c } from 'x'`) and Python (`from x import a, b, c`) can bundle many symbols from one module into a single import line, but Kotlin has no equivalent grouping syntax and idiomatic style (ktlint's `no-wildcard-imports`) forbids collapsing them with `import x.*`, so each symbol needs its own line. Counting raw lines would flag a Kotlin file pulling many symbols from a handful of packages as far more sprawling than an equivalent TS/Python file with identical actual coupling.

## Example

```python
# utils.py, 9 unrelated helper functions, well past the util-file threshold of 8
def parse_date(s): ...
def format_currency(v): ...
def slugify(s): ...
def retry(fn): ...
def hash_password(p): ...
def send_email(to, body): ...
def resize_image(img): ...
def validate_email(s): ...
def flatten(lst): ...
```

## Configuration

- `energyStateAnalyzer.coherence.largeFunctionLines` (default `20`), line count above which a function counts as "large."
- `energyStateAnalyzer.coherence.maxLargeFunctions` (default `5`), number of large functions a file can contain before it's flagged.
- `energyStateAnalyzer.coherence.singleDomainNameShare` (default `0.7`), share of a file's functions that must share a leading name word to be treated as one coherent domain.
- `energyStateAnalyzer.coherence.maxTypeDiversityRatio` (default `0.4`), maximum ratio of distinct parameter/return base types to typed functions for a file to be treated as one type-cohesive module.
- `energyStateAnalyzer.coherence.minTypedCoverage` (default `0.5`), minimum share of a file's functions that must carry an explicit type annotation before `maxTypeDiversityRatio` is trusted; below this the detector falls back to `singleDomainNameShare`.

The function-count sprawl thresholds themselves (8 for utils-named files, 12 generic, 15 for high severity) and the import-sprawl thresholds (10/15) are fixed heuristics, not exposed as settings.

### Known limitations of the type-cohesion signal

- Wrapper generics (`Optional[str]`, `Dict[str, int]`) normalize to their wrapper base (`Optional`, `Dict`), not the wrapped domain type — same for F#'s postfix `int option` syntax, which has no bracket at all and produces no signal. Unwrapping common wrappers per language would reopen the per-language special-casing the shared, text-based normalizer is designed to avoid.
- Function-shaped parameter types (callbacks) and TypeScript's array shorthand (`number[]`, as opposed to `Array<number>`) don't contribute a signal either — the former because a callback parameter's type says nothing about a function's own domain, the latter because it isn't the bracket syntax the normalizer looks for.
