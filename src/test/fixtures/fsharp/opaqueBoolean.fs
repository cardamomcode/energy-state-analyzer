module OpaqueBoolean

let flaggedPositionalBoolean () =
    configure true

let flaggedPositionalBooleanAmongOthers () =
    process 1 false

let suppressedNamedArgument () =
    configure(retries = true)

let suppressedNonCallUsage () =
    let ok = true
    ok
