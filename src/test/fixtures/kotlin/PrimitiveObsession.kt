// clean — not flagged by primitive obsession
fun cleanDistinctTypes(name: String, age: Int): String {
    return "$name:$age"
}

// flagged — primitive obsession
fun flaggedSwapRisk(x: Int, y: Int): Int {
    return x + y
}

// flagged — primitive obsession (low)
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

// flagged — boolean blindness
fun flaggedBooleanBlindness(compress: Boolean, notify: Boolean): Boolean {
    return compress && notify
}
