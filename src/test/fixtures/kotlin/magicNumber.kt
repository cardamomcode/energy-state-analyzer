const val MAX_RETRIES = 5

fun cleanCommonValues(x: Int): Int {
    val total = x * 1
    return total + 0
}

fun flaggedMagicNumbers(price: Double): Double {
    var total = price * 1.08
    if (total > 50) {
        total += 15.75
    }
    return total
}

fun exemptIndexAndDefault(arr: IntArray, weight: Int = 42): Int {
    val first = arr[0]
    return first + weight
}

fun cleanNegativeValue(flag: Boolean): Int {
    if (flag) {
        return -1
    }
    return 1
}
