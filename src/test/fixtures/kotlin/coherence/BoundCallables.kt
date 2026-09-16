fun encode(value: Int): Int = value

val moduleBound = { value: String -> value }

fun outer(items: List<Boolean>): List<Boolean> {
    val localBound = { value: Boolean -> value }
    return items.map { item -> item }
}

class Operations {
    val classBound = { value: Double -> value }

    fun namedMethod(value: Long): CharSequence = value.toString()

    fun outerMethod(items: Set<Int>): Map<Int, Int> {
        val localBound = { value: Int -> value }
        return items.associateWith { item -> item }
    }
}
