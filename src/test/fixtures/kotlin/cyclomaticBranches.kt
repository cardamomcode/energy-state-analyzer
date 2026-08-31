fun classify(value: String): Int =
    when (value) {
        "a" -> 1
        "b" -> 2
        else -> 0
    }

fun classifyWithoutFallback(value: String): Int {
    return when (value) {
        "a" -> 1
        "b" -> 2
    }
}
