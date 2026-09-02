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

## Choose a fix that matches the cause

Do not automatically replace every long parameter list with one large object. That removes the positional call-site problem, but it can also hide unrelated inputs in a "parameter bag." Start by asking why the values travel together:

- **Introduce a parameter object** when the values describe one operation and callers normally supply them together. Give the object a meaningful name, such as `CreateUser` or `RetryPolicy`, rather than `Args` or `Options` when it represents domain data.
- **Use a domain/value type** when several values are one concept, such as an address, date range, or contact details. Put validation and behavior that belong to that concept on the type.
- **Pass an existing cohesive object** when the caller already has the domain object that the operation needs. Do not do this merely to avoid parameters: it can couple the callee to data it does not need.
- **Split the operation** when its arguments expose several responsibilities, for example inventory, payment, notification, and auditing. Splitting fixes the responsibility problem; an options object alone only hides it.
- **Move stable collaborators onto the owning object** when the repeated parameters are dependencies such as repositories, clocks, or payment services. Keep the method's actual operation inputs as parameters. This is not a reason to conceal dependencies that vary per call.
- **Use a builder** when construction is incremental, conditional, or must validate combinations of optional values. A builder is usually unnecessary when one complete value can be created directly.

Named arguments, keyword-only parameters, and default values can make a call easier to read, but they do not reduce the declared parameter count. They therefore do not stop this detector from reporting a long signature. Group related values or split the operation when the count itself needs to fall.

If the parameters are adjacent values of the same primitive type, the central risk may be that callers can silently swap them. See [Primitive Obsession](primitive-obsession.md) for distinct domain types that make those calls harder to misuse.

## Language-specific idioms

### TypeScript: interfaces for input shapes, classes for owned dependencies

Use an interface or type alias for a cohesive input shape. Prefer several small domain types to one flat object when the groups have independent meaning. Constructor injection is appropriate when an object owns stable collaborators.

```typescript
interface Address {
  city: string;
  country: string;
}

interface ContactInfo {
  email: string;
  phone: string;
}

interface CreateUser {
  name: string;
  age: number;
  address: Address;
  contact: ContactInfo;
}

function createUser(user: CreateUser): void {
  // ...
}

class OrderProcessor {
  constructor(
    private readonly payment: PaymentService,
    private readonly inventory: Inventory,
  ) {}

  process(order: Order, customer: Customer): void {
    // ...
  }
}
```

### Python: dataclasses for related data; keyword-only parameters for clarity

Use a dataclass for related values that are passed through the domain together. A keyword-only signature improves a small API's call sites, but is only a readability aid when it still has many declared parameters.

```python
from dataclasses import dataclass


@dataclass(frozen=True)
class Address:
    city: str
    country: str


@dataclass(frozen=True)
class CreateUser:
    name: str
    age: int
    address: Address
    email: str
    phone: str


def create_user(user: CreateUser) -> None:
    ...


def find_users(*, city: str, country: str, active_only: bool) -> list[CreateUser]:
    ...  # explicit names help here; this smaller signature is not a parameter object
```

### Kotlin: data classes for grouped values

Use `data class` for a cohesive value. Kotlin named arguments and default values are useful at call sites, but a function with six declared parameters remains a six-parameter function and is still reported.

```kotlin
data class Address(val city: String, val country: String)
data class ContactInfo(val email: String, val phone: String)
data class CreateUser(val name: String, val age: Int, val address: Address, val contact: ContactInfo)

fun createUser(user: CreateUser) {
    // ...
}
```

### C++: small structs for cohesive inputs

Use a small `struct` for related inputs and pass it by `const` reference when copying is not the intended API cost. C++20 designated initializers can make aggregate construction explicit; on earlier standards, initialize a local object by field name rather than relying on a long positional aggregate initializer.

```cpp
struct Address {
    std::string city;
    std::string country;
};

struct CreateUser {
    std::string name;
    int age;
    Address address;
    std::string email;
    std::string phone;
};

void createUser(const CreateUser& user);

CreateUser user;
user.name = "Alice";
user.age = 30;
user.address = {"Oslo", "Norway"};
createUser(user);
```

### F#: records for values and partial application for stable dependencies

Use records to make related values one explicit input. When dependencies are stable, accept them first and partially apply the function; the resulting function exposes only the operational input. Currying is not a substitute for reducing an unrelated long argument list.

```fsharp
type Address =
    { City: string
      Country: string }

type CreateUser =
    { Name: string
      Age: int
      Address: Address
      Email: string
      Phone: string }

type Dependencies = { SaveUser: CreateUser -> unit }

let createUser (dependencies: Dependencies) (user: CreateUser) =
    dependencies.SaveUser user

let createUserInStore = createUser dependencies
```

## Configuration

The thresholds are configured at the same three levels as every other detector (see [Configuration](../configuration.md)): built-in defaults, a project's `.esaconfig.json`, and a host override (VS Code settings or CLI flags). The defaults are `5` (medium) / `8` (high).

```jsonc
{
  "parameterCount": { "mediumThreshold": 5, "highThreshold": 8 }
}
```

In the editor this is `energyStateAnalyzer.parameterCount.mediumThreshold` / `.highThreshold`; in the CLI, `--medium-parameter-count N` / `--high-parameter-count N`. Each flag overrides only the value it provides.

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
