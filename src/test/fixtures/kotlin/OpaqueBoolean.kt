fun flaggedPositionalBoolean() {
    configure(true)
}

fun flaggedPositionalBooleanAmongOthers() {
    process(1, false)
}

fun suppressedNamedArgument() {
    configure(retries = true)
}

fun suppressedNonCallUsage(): Boolean {
    return true
}
