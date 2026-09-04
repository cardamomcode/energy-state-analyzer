fun cleanDistinctTypes(name: String, age: Int): String {
    return "$name:$age"
}

fun flaggedSwapRisk(x: Int, y: Int): Int {
    return x + y
}

fun flaggedStringlyTyped(status: String): Int {
    if (status == "pending") {
        return 1
    } else if (status == "active") {
        return 2
    } else if (status == "closed") {
        return 3
    }
    return 0
}
