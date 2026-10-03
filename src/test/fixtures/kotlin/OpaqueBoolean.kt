// flagged — opaque boolean
fun flaggedPositionalBoolean() {
    configure(true)
}

// flagged — opaque boolean
fun flaggedPositionalBooleanAmongOthers() {
    process(1, false)
}

// flagged in production; intentional expected values in test files
fun flaggedAssertionBoolean() {
    assertEquals(true, isEnabled())
    assertEquals(false, isDisabled())
}

// clean — not flagged by opaque boolean
fun suppressedNamedArgument() {
    configure(retries = true)
}

// clean — not flagged by opaque boolean
fun suppressedNonCallUsage(): Boolean {
    return true
}
