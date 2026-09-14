module OpaqueBoolean

// flagged — opaque boolean
let flaggedPositionalBoolean () = configure true

// flagged — opaque boolean
let flaggedPositionalBooleanAmongOthers () = process 1 false

// clean — not flagged by opaque boolean
let suppressedNamedArgument () = configure (retries = true)

// clean — not flagged by opaque boolean
let suppressedNonCallUsage () =
    let ok = true
    ok
