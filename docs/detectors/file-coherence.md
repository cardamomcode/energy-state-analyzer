# File Coherence

Flags files that have lost a single responsibility, the "utils/helpers sprawl" pattern, along four independent signals.

## What it flags

**Class methods are grouped by their enclosing class, not counted as free-standing functions.** Every signal below that operates on "functions" (function-count sprawl and its type-cohesion check) only ever sees module-level, free-standing functions — a class's methods are excluded and evaluated instead by the class-relatedness check further down. This matters for OOP-style files (Python/TypeScript/Kotlin/C++; F# has no class construct this detector models): without it, a file of two small, tightly related classes with 15 methods total between them reads exactly like 15 unrelated free-standing functions, and the type-cohesion signal — which only sees each method's own explicitly-annotated parameter/return types, not the fact that the method belongs to one of only two classes — misreads the handful of incidental types (mostly the classes' own names, showing up in constructors and factory-style methods) as "unrelated types" sprawl. A class's *own* method count and responsibility are judged separately by the god-class check further down — see [God class](god-class.md), which flags one type whose methods span too many unrelated domains rather than counting classes or free-standing functions.

**Function-count sprawl.** A file with more than 12 functions is flagged (medium; high past 15). The threshold drops to 8 if the filename itself contains `util`, `helper`, or `common`, treating the name as a proxy for "already known to be a grab-bag." The violation message deliberately steers away from "just split this file" — since the detector only sees one file, it can't tell whether a split would land the code in an existing cohesive module or just distribute the same coupling across new files with more imports wiring them back together.

A file is exempted from this check regardless of count if either of two independent cohesion signals says otherwise (unless the filename already admits to being utils/helper/common, which overrides both):

- *Naming cohesion*: most of its functions share a leading name word (e.g. `extractFoo`/`extractBar`/`extractBaz`, at least a 70% share by default) — one coherent domain broken into small steps, not a grab-bag.
- *Type cohesion*: among the file's typed functions (those with at least one typed parameter or return-type annotation), the ratio of distinct base types touched to typed functions is at or below 0.4 by default. This catches a pattern naming cohesion misses entirely: an F#-style module exposing one verb per operation (`map`/`filter`/`fold`/`zip`/`scan`/...) shares no name prefix at all, but nearly every function touches the module's own type family (confirmed against a real example, `expression/collections/seq.py`, whose ~97 functions almost all touch `Iterable`/`Seq`/`Iterator`). The ratio is deliberately about *type reuse*, not one type reaching a majority share — a cohesive module often legitimately spans a family of related types, and requiring one to dominate false-negatived on exactly that case. Type cohesion is checked first (stronger signal, not vulnerable to name-prefix coincidence); the naming heuristic is only consulted when there's too little type-annotation coverage to trust the ratio (fewer than half the file's functions carry any type annotation, by default).

If a file is still going to be flagged and its type signal is confidently measured as *diverse* (real type coverage, no shared type family), that's treated as authoritative over naming — a consistent naming convention (`parse_date`/`parse_json`/`parse_csv`) touching unrelated types is still a real entropy dump — and produces a distinct, stronger message naming how many unrelated types the file spans. This is only ever evaluated at the same 8/12 function-count thresholds as everything else above, not an earlier one: a lower threshold was tried and rejected after it produced a real false positive on this project's own `coherence.ts` (a handful of purpose-cohesive helper functions using a few different supporting types, not a grab-bag) — type diversity alone isn't a reliable enough signal below ~12 functions to tell the two apart.

**Large-function sprawl.** Counted independently of the check above, on the theory that a module with 30 small functions is fine but one with 6 sprawling ones isn't. A file with more than 5 functions exceeding 20 lines is flagged (medium; high past 7.5 large functions). This gates on large-function count rather than raw function count so that languages like F#, which idiomatically have many small functions per module, aren't penalized just for having a lot of them.

**Dependency breadth.** A file drawing from more than 10 distinct modules/packages is flagged (medium; high past 15). Counts distinct import *sources*, not raw import lines/symbols, since that measures independent dependencies rather than syntax: TS and Python can bundle several bindings in one statement, while Kotlin commonly imports one declaration per line. C++ headers participate only in this signal.

The count is a dependency-surface signal, not proof that the file has multiple responsibilities. A cohesive cross-cutting test or composition root can legitimately use many dependencies, especially when one capability is exposed through several sibling modules. In that case, first consider a focused facade or qualified access through the common parent; do not split the file solely to reduce the count. A facade is appropriate only when its exports form a coherent capability — a catch-all re-export module just hides genuine coupling.

**Import member fan-out.** Ten or more explicitly imported declarations from one source are flagged, even when that source counts as only one dependency. This identifies a wide local vocabulary from one API area, which is common in Kotlin's one-declaration-per-import style and can also arise from Python `from` imports or TypeScript named imports. The message asks whether the file is an intentional composition boundary; it does not prescribe a split or a wildcard import.

**Import scope pollution.** A wildcard import is flagged directly because it makes an external scope available without qualification. For F# specifically, the detector also reports **Import scope sprawl** when seven or more opened modules share a parent namespace: `open` brings each module's values into lexical scope, so names can shadow one another and an unqualified reference obscures its origin. Prefer qualified access (for example, `Energy.Languages.Python.pythonLanguageAdapter`) or a small named module alias. C++ `#include` does not have this meaning; `using namespace` is its corresponding scope construct and is outside this detector's current grammar coverage.

**Class relatedness (Python/TypeScript/Kotlin/C++).** A file containing 2 or more classes or structs is flagged when those types split into multiple families with nothing connecting them — no shared inheritance, no method signature referencing another type in the file, and no shared naming affix. Unlike the checks above, there's no minimum class count before this can fire: a class or struct is already a much stronger unit of cohesion than a single function (a whole type, not one operation), so even 2 completely unrelated types are worth flagging.

Two classes are linked into the same family by any of:

- *Direct inheritance* - one class's base name is the other's own name.
- *Shared base* - both classes extend/implement the same name, even one not defined in this file at all (e.g. a whole `exceptions.py`-style file of classes that all extend `Exception` but never reference each other).
- *Type cross-reference* - a method's signature (parameter or return type) names another class defined in the file, as with a token/token-source pair where one constructs or returns the other.

If the resulting graph still splits into more than one family, a naming-affix fallback (shared prefix or suffix across class names, e.g. `FooError`/`BarError`) gets one last chance to treat the whole file as one family before it's flagged. Unlike the function-count check's type-diversity signal, an unconnected class graph is only an absence of positive evidence, not a positive measurement of diversity - so it isn't treated as authoritative over naming.

The single-class-responsibility angle — a "god class" carrying too much by itself — is now covered by the [god class](god-class.md) sub-check, which flags one type whose methods span too many unrelated domains. It explicitly does *not* flag a stateless value type with a rich but cohesive combinator API (like an `Option` of combinators), since that is cohesion rather than sprawl.

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
- `energyStateAnalyzer.coherence.siblingOpenThreshold` (default `7`), minimum number of opened modules sharing a parent namespace before F# import scope sprawl is flagged. Raising it relaxes the sibling-open check; lowering it tightens it.
- `energyStateAnalyzer.coherence.importBreadthThreshold` (default `10`), minimum number of distinct modules a file draws from before "import sprawl" is flagged. Raising it relaxes the breadth check; lowering it tightens it.
- `energyStateAnalyzer.coherence.highImportBreadthThreshold` (default `15`), distinct-module count above which import sprawl is reported at high severity.
- `energyStateAnalyzer.coherence.memberImportFanOutThreshold` (default `10`), minimum number of declarations imported from a single source before "import member fan-out" is flagged. Raising it relaxes the fan-out check; lowering it tightens it.

The function-count sprawl thresholds themselves (8 for utils-named files, 12 generic, 15 for high severity) are fixed heuristics, not exposed as settings. The import signals are configurable via `siblingOpenThreshold`, `importBreadthThreshold`/`highImportBreadthThreshold` (distinct-module breadth and its high-severity cutoff), and `memberImportFanOutThreshold`; wildcard imports are structural findings and need no count threshold.

### Known limitations of the type-cohesion signal

- Wrapper generics (`Optional[str]`, `Dict[str, int]`) normalize to their wrapper base (`Optional`, `Dict`), not the wrapped domain type — same for F#'s postfix `int option` syntax, which has no bracket at all and produces no signal. Unwrapping common wrappers per language would reopen the per-language special-casing the shared, text-based normalizer is designed to avoid.
- Function-shaped parameter types (callbacks) and TypeScript's array shorthand (`number[]`, as opposed to `Array<number>`) don't contribute a signal either — the former because a callback parameter's type says nothing about a function's own domain, the latter because it isn't the bracket syntax the normalizer looks for.
- C++ qualified names such as `std::string` and generic heads such as `std::vector<T>` are recognized,
  but aliases, concepts, deduced `auto`, and types created through macros are not resolved semantically.
