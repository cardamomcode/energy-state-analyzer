# Logical Operator as Control Flow

Flags a bare `condition && doSomething()` (or `condition || fallback()`) used as a standalone statement, an `if` hidden behind a boolean operator instead of written as one.

## What it flags

This is legal in every language whose grammar has a statement-level boolean expression, including Python's bare `and`/`or` expression statement, not just TypeScript's `&&`/`||`. It already counts toward [cyclomatic complexity](cyclomatic-complexity.md), since a boolean operator is a decision point there too; this detector exists only to name the *readability* cost separately. An if hidden as an expression is invisible to anyone skimming for branches, and can't grow past a single consequent expression without becoming unreadable.

Runs on Python and TypeScript. Not on F#, which has no such statement-level idiom in its grammar.

## Example

```typescript
isValid && submit();       // flagged: if-statement disguised as '&&'
retries || fallback();     // flagged: if-statement disguised as '||'
```

```typescript
if (isValid) {
  submit();
}
```

is the suggested rewrite in both cases.
