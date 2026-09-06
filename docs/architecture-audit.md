# Architecture conformance

`--architecture` performs an architecture conformance audit against explicit repository-layer
boundaries, using imports already recognized by the analyzer's Tree-sitter language adapters. It
is an opt-in CLI/CI command: it does not add editor diagnostics or alter ordinary scan/SARIF
results.

```bash
npx energy-state-analyzer --architecture --report human
npx energy-state-analyzer --architecture src --report json
```

The audit reads `.esa-architecture.json` from the directory where the command runs. It exits 0
when every configured boundary is respected, 1 when a forbidden dependency is found, and 2 when
the policy is absent or invalid.

## Policy

```json
{
  "zones": [
    {
      "name": "Core",
      "files": ["src/Core"],
      "importPrefixes": ["Energy.Core"]
    },
    {
      "name": "Extension",
      "files": ["src/Extension"],
      "importPrefixes": ["Energy.Extension"]
    }
  ],
  "forbiddenDependencies": [
    { "from": "Core", "to": "Extension" }
  ]
}
```

`files` entries are exact relative paths or directory prefixes. `importPrefixes` match an import
source exactly or as a dot/slash-qualified descendant. Each forbidden edge names two configured
zones. Imports that cannot be assigned to both zones are reported as unresolved/external in JSON
but never treated as violations.

This deliberately evaluates direct, source-level imports only. It does not infer call graphs,
resolve types, or report dependency cycles; those require conservative per-language resolution
before they can be trustworthy.

## Why boundaries matter

Architectural boundaries contain change. By restricting permitted dependencies, they reduce the
paths through which a change in one module can affect another. A forbidden dependency opens a new
coupling path across an intended containment boundary.

This measures architecture conformance at a different scale from the analyzer's file- and
function-level checks: local detectors identify complexity within code, while this audit identifies
dependencies through which complexity and change can propagate between modules. This follows the
information-hiding view that interfaces act as a firewall against change propagation; accumulated
boundary violations are one manifestation of architecture erosion. See [Reflections on Classical
and Nonclassical Modularity](https://www.cs.cmu.edu/~ckaestne/pdf/ecoop11.pdf) and [Understanding
Software Architecture Erosion: A Systematic Mapping Study](https://arxiv.org/abs/2112.10934).

Energy and entropy are useful conceptual terms here: boundaries limit the dependency arrangements
and change paths that developers must consider. This audit does not calculate architectural energy
or entropy; it reports direct, configured forbidden dependencies.
