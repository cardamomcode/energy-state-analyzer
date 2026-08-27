# File Coherence

Flags files that have lost a single responsibility, the "utils/helpers sprawl" pattern, along three independent signals.

## What it flags

**Function-count sprawl.** A file with more than 12 functions is flagged (medium; high past 15). The threshold drops to 8 if the filename itself contains `util`, `helper`, or `common`, treating the name as a proxy for "already known to be a grab-bag." A file is exempted from this check regardless of count if most of its functions share a leading name word (e.g. `extractFoo`/`extractBar`/`extractBaz`, at least a 70% share by default): that's treated as one coherent domain broken into small steps, not a grab-bag of unrelated helpers, unless the filename already admits to being utils/helper/common, which overrides the naming signal.

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

The function-count sprawl thresholds themselves (8 for utils-named files, 12 generic, 15 for high severity) and the import-sprawl thresholds (10/15) are fixed heuristics, not exposed as settings.
