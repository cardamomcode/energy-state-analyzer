# Parameter Explosion

Flags functions with too many parameters for a caller to reliably remember the order and meaning of.

## What it flags

Functions with more than 5 parameters are flagged (medium; high past 8). Beyond roughly 5 parameters, callers typically can no longer recall argument order or meaning without checking the signature. Both thresholds are configurable — see [Configuration](../configuration.md).

## Example

```typescript
function createUser(name: string, email: string, age: number, city: string, country: string, phone: string) {
  // flagged: 6 parameters
}
```

The usual fix is grouping related parameters into an object, or a builder pattern.

## Configuration

The thresholds are configured at the same three levels as every other detector (see [Configuration](../configuration.md)): built-in defaults, a project's `.esaconfig.json`, and a host override (VS Code settings or CLI flags). The defaults are `5` (medium) / `8` (high).

```jsonc
{
  "parameterCount": { "mediumThreshold": 5, "highThreshold": 8 }
}
```

In the editor this is `energyStateAnalyzer.parameterCount.mediumThreshold` / `.highThreshold`; in the CLI, `--medium-parameter-count N` / `--high-parameter-count N`. Each flag overrides only the value it provides.

## Known limitations

TypeScript arrow functions and C++ lambdas aren't analyzed by this detector, only named functions and methods; Python's `lambda` has the same gap. C++ parameter packs count when the grammar exposes them as parameter declarations, but macro-generated parameters are invisible without preprocessing.
