// flagged — opaque boolean
fun flaggedPositionalBoolean() {
    configure(true)
}

// flagged — opaque boolean
fun flaggedPositionalBooleanAmongOthers() {
    process(1, false)
}

// clean — not flagged by opaque boolean
fun suppressedNamedArgument() {
    configure(retries = true)
}

// clean — not flagged by opaque boolean
fun suppressedNonCallUsage(): Boolean {
    return true
}
