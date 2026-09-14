// clean — not flagged by cyclomatic complexity
fun classify(value: String): Int =
    when (value) {
        "a" -> 1
        "b" -> 2
        else -> 0
    }

// clean — not flagged by cyclomatic complexity
fun classifyWithoutFallback(value: String): Int {
    return when (value) {
        "a" -> 1
        "b" -> 2
    }
}
