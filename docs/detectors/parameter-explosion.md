# Parameter Explosion

Flags functions with too many parameters for a caller to reliably remember the order and meaning of.

## What it flags

Functions with more than 5 parameters are flagged (medium; high past 8). Beyond roughly 5 parameters, callers typically can no longer recall argument order or meaning without checking the signature.

## Example

```typescript
function createUser(name: string, email: string, age: number, city: string, country: string, phone: string) {
  // flagged: 6 parameters
}
```

The usual fix is grouping related parameters into an object, or a builder pattern.

## Known limitations

The threshold is not yet configurable via VS Code settings; it's fixed at >5 (medium) / >8 (high). TypeScript arrow functions aren't analyzed by this detector, only named `function` declarations and class methods; Python's `lambda` has the same gap.
