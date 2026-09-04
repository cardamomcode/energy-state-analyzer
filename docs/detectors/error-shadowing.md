# Error Shadowing

Flags functions where error handling occupies so much of the body that the happy path is difficult to see. This is a cohesion signal, not another complexity score: a function can have few branches yet still mix recovery policy and business work into one hard-to-read unit.

## What it flags

The detector measures the share of a function's named syntax nodes that occur inside a `try` construct or its `catch`, `except`, or `finally` arms. It reports a finding when that share reaches the configured threshold and the function contains enough named nodes to make the proportion meaningful.

The finding points at the first error-handling construct, rather than the function declaration, so the reader lands on the region doing the shadowing. A function without error handling is never flagged, even if its configured threshold is `0`.

## Example

```python
def load_profile(user_id):
    try:
        response = client.fetch(user_id)
        profile = decode(response)
        validate(profile)
        save(profile)
        return profile
    except NetworkError:
        retry_later(user_id)
    except DecodeError:
        record_bad_response(user_id)
    finally:
        metrics.flush()
```

The fetch, decoding, validation, persistence, recovery, and cleanup policy are all interleaved under one error-handling region. Extracting the happy path or moving recovery into a boundary-specific helper makes each concern easier to follow.

## Configuration

- `energyStateAnalyzer.errorShadowing.enabled` (default `true`) enables or disables the detector in VS Code.
- `errorShadowing.threshold` (default `0.5`) is the medium-severity share in `.esaconfig.json`.
- `errorShadowing.highThreshold` (default `0.7`) is the high-severity share in `.esaconfig.json`.
- `errorShadowing.minNamedNodes` (default `8`) avoids reporting tiny wrappers whose ratio is not meaningful.

The editor and CLI share the three detail values through `.esaconfig.json`. An explicitly configured VS Code setting such as `energyStateAnalyzer.errorShadowing.threshold` overrides the file for that workspace; an unmodified VS Code default does not. See [Configuration](../configuration.md) for the complete schema and precedence.

## Known limitations

This is syntax-only analysis. It does not know whether a `catch` is reachable, whether a helper can throw, or whether a `finally` arm is operationally essential. It measures named syntax nodes rather than language-specific statements so that the same approximation works across Python, TypeScript, F#, Kotlin, and C++.

As a result, deeply nested expressions and declarations count toward the share, while punctuation and other unnamed grammar tokens do not. Treat a finding as a prompt to examine responsibility boundaries, not proof that every `try` block should be split.
