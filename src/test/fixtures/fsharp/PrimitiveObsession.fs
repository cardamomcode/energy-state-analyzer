module PrimitiveObsession

let cleanDistinctTypes (name: string) (age: int) = sprintf "%s:%d" name age

let flaggedSwapRisk (x: int) (y: int) = x + y

let flaggedStringlyTyped (status: string) =
    if status = "pending" then 1
    elif status = "active" then 2
    elif status = "closed" then 3
    else 0

// Bug 1 (and-binding, false positive): two mutually recursive heads sharing a variable name must NOT
// accumulate literals into one stringly-typed finding — each head is analyzed in isolation (2 and 1
// distinct literals, neither reaching the threshold of 3).
let rec cleanAndBindingHead (x: string) = if x = "aa" || x = "bb" then 1 else 0
and cleanAndBindingTail (x: string) = if x = "cc" then 1 else 0

// Bug 1 (and-binding, false negative): the second head's parameters ARE analyzed for swap risk —
// (x: int) (y: int) is the same primitive-swap shape as flaggedSwapRisk above.
let rec andBindingAlpha (a: int) = a
and flaggedAndBindingPair (x: int) (y: int) = x + y

// Bug 1 (and-binding, still flagged): a head with a genuinely stringly-typed dispatch is still caught
// — the split must not introduce a false negative for a real 3-literal dispatch.
let rec flaggedAndBindingStrings (s: string) =
    if s = "aa" then 1
    elif s = "bb" then 2
    elif s = "cc" then 3
    else 0

and andBindingStringsTail (t: string) = if t = "zz" then 1 else 0

// Bug 2 (postfix-type barrier): a `string option` between two `string` parameters is a distinct-type
// barrier, so `a` and `c` are not "consecutive" and the swap-risk premise (they can be transposed
// positionally) does not hold.
let cleanPostfixTypeBarrier (a: string) (b: string option) (c: string) = a + c

// Bug 3 (named-argument calls): a named `name = "..."` argument is not a string equality comparison,
// so `name` is not counted as stringly-typed even though three distinct literals appear.
let cleanNamedArgumentCalls (n: int) =
    let _ = setField (name = "alpha")
    let _ = setField (name = "beta")
    let _ = setField (name = "gamma")
    n

// Gap 1 (match on strings): the idiomatic match dispatch on string literals is flagged the same way
// the equivalent if/elif chain (flaggedStringlyTyped above) is.
let flaggedMatchStringDispatch (status: string) =
    match status with
    | "pending" -> 1
    | "active" -> 2
    | "closed" -> 3
    | _ -> 0
