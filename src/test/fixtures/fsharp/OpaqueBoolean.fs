module OpaqueBoolean

// flagged — opaque boolean
let flaggedPositionalBoolean () = configure true

// flagged — opaque boolean
let flaggedPositionalBooleanAmongOthers () = process 1 false

// flagged in production; intentional expected values in test files
let flaggedAssertionBoolean () =
    Assert.Equal(isEnabled (), true)
    Assert.Equal(isDisabled (), false)

// clean — not flagged by opaque boolean
let suppressedNamedArgument () = configure (retries = true)

// clean — not flagged by opaque boolean
let suppressedNonCallUsage () =
    let ok = true
    ok
