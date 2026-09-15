fun named(value: Int): Int = value

val moduleBound = { left: Int, right: Int -> left + right }

class Operations {
    val classBound = { value: Int -> value + 1 }

    fun namedMethod(value: Int): Int = value
}

fun outer(items: List<Int>): List<Int> {
    val localBound = { value: Int -> value * 2 }
    return items.map { it + 1 }
}
