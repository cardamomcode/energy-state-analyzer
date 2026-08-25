fun cleanMixedConditions(a: Int, b: String, c: String?): Int {
    if (a > 10) {
        return 1
    } else if (b == "urgent") {
        return 2
    } else if (c == null) {
        return 3
    }
    return 0
}

fun flaggedThreeWayChain(status: String): Int {
    if (status == "open") {
        return 1
    } else if (status == "closed") {
        return 2
    } else if (status == "pending") {
        return 3
    }
    return 0
}
