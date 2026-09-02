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

The right fix depends on *why* the list is large. Grouping everything into one object silences the smell but doesn't always address it — pick what matches the cause:

- **Named options object** — the parameters belong to a single operation and are passed together at every call site. This directly removes caller-side ambiguity, since argument order no longer has to be memorized.
- **Domain / value type** — several parameters already describe one concept (an address, contact details). Grouping them into a named type reduces *conceptual* complexity, not just the positional count.
- **Split the function** — the parameters reveal multiple responsibilities (inventory, payment, notifications, auditing in one call). An options object mutes the warning; separating concerns fixes it.
- **Move repeated dependencies onto an object** — when several parameters are services or state passed together at every call site, constructor or field injection turns them into state and leaves the real operation inputs visible.
- **Builder** — construction is incremental, optional, conditional, or otherwise complex. Reach for a builder because of that complexity, not merely because there are many parameters.

Some mitigations improve call-site readability without lowering the count — e.g. Kotlin/Python named arguments or default values — so this detector still fires on them; reach for grouping or splitting when you need the number of parameters itself to drop.

Prefer a domain type over mechanically spreading six positional arguments into six object fields: it reads better at the call site *and* collapses related values into one concept. If most of the parameters are same-typed primitives (e.g. six booleans), positional ambiguity is really the smell — see [Primitive Obsession](primitive-obsession.md), which flags adjacent parameters that share a primitive type and can be silently swapped.

### Per-language idioms

**TypeScript.** An options `interface` (nested interfaces for grouped values) is the default; for injected dependencies, move them to the constructor and keep operation parameters on the method.

```typescript
// options object — order no longer has to be memorized
function createUser(options: CreateUserOptions) {}

// domain types reduce conceptual complexity further
interface Address { city: string; country: string }
interface ContactInfo { email: string; phone: string }
function createUser(user: { name: string; age: number; address: Address; contact: ContactInfo }) {}

// dependencies become state, operation inputs stay visible
class OrderProcessor {
  constructor(private payment: PaymentService, private inventory: Inventory) {}
  process(order: Order, customer: Customer) {}
}
```

**Python.** Keyword arguments (optionally keyword-only with `*`) let callers name each value; bundle related config into a `@dataclass` when it is threaded through several functions.

```python
def create_user(*, name, email, age, city, country, phone): ...   # named at the call site

@dataclass
class ContactInfo:
    email: str
    phone: str
def create_user(user: CreateUser): ...   # grouped values in a record
```

**Kotlin.** Group related values into data classes to actually cut the count — that is the real fix here. Named arguments with default parameters make each call site unambiguous, but they don't reduce the declared parameter count, so this detector still fires on them.

```kotlin
data class Address(val city: String, val country: String)
data class ContactInfo(val email: String, val phone: String)
fun createUser(name: String, age: Int, address: Address, contact: ContactInfo)   // 4 params — not flagged

// named args + defaults improve call-site clarity but keep all six parameters (still flagged):
fun createUser(
    name: String, email: String, age: Int = 0,
    city: String = "", country: String = "", phone: String = ""
) // create_user(city = "Oslo", country = "Norway")
```

**C++.** Pass an options struct by value (aggregate-initializable at the call site); use a builder or fluent interface when construction is conditional or incremental.

```cpp
struct CreateUserOptions { std::string name; std::string email; std::string phone; };
void createUser(CreateUserOptions opts);   // called with aggregate init: createUser({ .name = "Alice", ... })

// dependencies become state, operation inputs stay visible
class OrderProcessor {
public:
    explicit OrderProcessor(PaymentService p, Inventory i)
        : payment_(std::move(p)), inventory_(std::move(i)) {}
    void process(const Order& order, const Customer& customer);
private:
    PaymentService payment_;
    Inventory inventory_;
};
```

**F#.** Record types are the natural grouping, and F#'s currying turns a long parameter list into a chain of unary functions — so dependencies can be injected first and partially applied to yield a function of just the operational parameters, instead of threading an options bag everywhere.

```fsharp
type createUserOptions = { name: string; email: string; age: int; city: string; country: string; phone: string }
let createUser ({ name; email; _ } : createUserOptions) = ...

// grouped domain values
type address = { city: string; country: string }
let createUser (user: CreateUser) = user.name, user.address.city

// inject dependencies first, curry to a function of the operational params only
let createUser (deps: Deps) ({ name; email; _ } : createUserOptions) = ...
let createForUser = createUser someDeps   // : createUserOptions -> _
```

## Known limitations

The threshold is not yet configurable via VS Code settings; it's fixed at >5 (medium) / >8 (high). TypeScript arrow functions and C++ lambdas aren't analyzed by this detector, only named functions and methods; Python's `lambda` has the same gap. C++ parameter packs count when the grammar exposes them as parameter declarations, but macro-generated parameters are invisible without preprocessing.
