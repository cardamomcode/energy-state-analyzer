# Primitive Obsession

Flags strings and numbers standing in for what should be a distinct, validated type. Two independent sub-checks, both driven through the same per-language traversal.

## What it flags

**Parameter-swap risk.** Two adjacent parameters sharing the same unqualified primitive type (e.g. `lat: float, lon: float`) are indistinguishable at the call site: nothing stops a caller from passing them in the wrong order. Runs on Python, F#, TypeScript, Kotlin, and C++. C++ pointer, reference, array, and function-declarator shapes remain part of the extracted type identity, so `int`, `int*`, and `int&` do not collide.

In Python, a pair is suppressed only when *both* parameters are keyword-only (after a bare `*` or `*args` in the signature), since the signature itself then makes a positional call impossible. Named-parameter naming is still a weaker mitigation than a distinct type (`NewType`, a dataclass, etc.), since nothing stops a future `**kwargs`-splat call from transposing the values by hand, but that gap isn't worth detecting. This suppression doesn't apply to TypeScript, Kotlin, or C++, which have no enforcing keyword-only boundary, or F#, whose named arguments are optional at the call site and so don't prevent a positional call.

**Stringly-typed control flow.** A variable compared against 3 or more distinct string literals within one function is a de facto enum encoded as strings, with no exhaustiveness checking and no typo protection at the type level. Runs on Python, F#, TypeScript, Kotlin, and C++; Python additionally flags a variable checked against a literal tuple/list/set in one `in` expression, since the other adapters have no modeled direct equivalent construct.

## Example

```python
def haversine(lat: float, lon: float, alt: float):
    # flagged: lat/lon and lon/alt are adjacent same-typed pairs a caller can swap
    ...

def handle(status: str):
    if status == "pending": ...
    elif status == "active": ...
    elif status == "closed": ...
    # flagged: 'status' compared against 3 distinct string literals
```

## Language-specific type advice

### Convert at the boundary

Keep primitives at the application or library boundary, not throughout the
domain. When data enters—from an HTTP request, CLI argument, configuration,
database row, or another library—validate it and construct the distinct domain
type immediately. When data leaves, explicitly serialize or unwrap that domain
type for the external contract.

This concentrates parsing, validation, and unavoidable casts in one place. The
rest of the domain API should accept and return `UserId`, `Quantity`, or
`Status`, rather than their underlying `int`, `number`, or `string`. Do not
repeatedly convert between primitive and domain types inside business logic:
that recreates the swap and invalid-value risks the distinct type is meant to
remove.

### Python: use `NewType` or a dataclass

`NewType` gives a static type checker a distinct type while retaining the
underlying value at runtime. It is the lightweight option for IDs and other
values whose invariant has already been established.

```python
from typing import NewType

UserId = NewType("UserId", int)
OrderId = NewType("OrderId", int)

def process_order(user_id: UserId, order_id: OrderId) -> None:
    ...

process_order(OrderId(5002), UserId(101))  # type error
```

Use a frozen dataclass when construction needs validation or the value carries
domain behavior. Use an `Enum` or `Literal` for a finite set of string states.

### F#: use single-case unions

Single-case discriminated unions make equal underlying primitives distinct at
compile time. Keep the case private when creation must validate an invariant.

```fsharp
type UserId = private UserId of int
type OrderId = private OrderId of int

module UserId =
    let create value =
        if value > 0 then Some(UserId value) else None

let processOrder (userId: UserId) (orderId: OrderId) =
    // ...
    ()
```

For stringly-typed control flow, prefer a discriminated union so pattern
matches are checked for exhaustiveness.

### TypeScript: use branded types

TypeScript uses structural typing, so ordinary values with the same primitive
type are interchangeable: a `number` user ID and a `number` order ID can be
passed in either order. For lightweight, compile-time protection, use a
**branded type** (also called a nominal or opaque type). It is TypeScript's
closest equivalent to Python's `NewType` or an F# single-case union, and the
brand is erased from the emitted JavaScript.

```typescript
type Brand<Value, Name> = Value & { readonly __brand: Name };

type UserId = Brand<number, "UserId">;
type OrderId = Brand<number, "OrderId">;

const toUserId = (value: number): UserId => value as UserId;
const toOrderId = (value: number): OrderId => value as OrderId;

function processOrder(userId: UserId, orderId: OrderId): void {
    // ...
}

const userId = toUserId(101);
const orderId = toOrderId(5002);

processOrder(orderId, userId); // type error: OrderId is not assignable to UserId
processOrder(userId, orderId); // OK
```

The `toUserId` and `toOrderId` helpers above are assertions: use them only
after establishing the relevant invariant. At an input boundary, parse and
validate first. For example, Zod can validate a positive integer and return a
branded value in one step:

```typescript
import { z } from "zod";

const UserIdSchema = z.number().int().positive().brand<"UserId">();
type UserId = z.infer<typeof UserIdSchema>;

const userId = UserIdSchema.parse(input);
```

Use a value object instead when the domain needs behavior or invariants that
must remain enforced after construction. A class or object with a private
constructor can validate once and expose operations such as `add`; it costs an
allocation, unlike a brand.

| Approach | Runtime overhead | Validation | Best for |
| --- | --- | --- | --- |
| Branded type | None | Compile-time only | Lightweight, distinct primitive types |
| Schema-validated brand | Parsing cost | Input-boundary validation | Forms, API payloads, and other external data |
| Value object | Object allocation | Runtime invariant enforcement | Domain behavior and richer rules |

For a finite set of string states, use a string-literal union or an enum and
handle it with an exhaustive `switch`.

### Kotlin: use value classes

An `@JvmInline value class` gives a domain primitive a separate Kotlin type
without requiring a wrapper allocation in its usual representation. A private
constructor keeps validation at the creation boundary.

```kotlin
@JvmInline
value class UserId private constructor(val value: Int) {
    companion object {
        fun create(value: Int): UserId {
            require(value > 0)
            return UserId(value)
        }
    }
}

@JvmInline
value class OrderId(val value: Int)

fun processOrder(userId: UserId, orderId: OrderId) { /* ... */ }
```

Use an `enum class` or sealed hierarchy for a finite set of string-like
states; `when` can then be exhaustive.

### C++: use small value types

A small `struct` with an `explicit` constructor makes domain values distinct
and prevents accidental implicit conversion from the underlying primitive.

```cpp
struct UserId {
    explicit UserId(int value) : value(value) {}
    int value;
};

struct OrderId {
    explicit OrderId(int value) : value(value) {}
    int value;
};

void processOrder(UserId userId, OrderId orderId);
```

Put validation and operations on the value type when the invariant needs to be
enforced. Use `enum class` instead of string literals for a closed set of
states; unlike an unscoped enum, its values do not implicitly convert to
integers.

## Known limitations

The `in (a, b, c)`-style membership check for stringly-typed control flow only runs on Python; F#'s grammar has no direct equivalent, TypeScript's idiom (`[...].includes(x)`) is a call expression rather than a comparison node, and common C++ container membership checks are library calls that require semantic resolution. Type aliases and macro-expanded declarations are not resolved.
