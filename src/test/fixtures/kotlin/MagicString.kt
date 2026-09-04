fun cleanValues(name: String, config: Map<String, Int>): Pair<String, Int?> {
    val message = "user $name not found"
    println("something went wrong")
    return Pair(message, config["timeout"])
}

fun cleanInterpolatedKey(config: Map<String, Int>, key: String): Int? {
    return config["${key}_value"]
}

fun flaggedMagicString(status: String): Int {
    if (status == "pending") {
        return 1
    }
    if (status == "pending") {
        return 2
    }
    return 0
}

fun flaggedDictKey(config: Map<String, Int>, other: Map<String, Int>): Int {
    return (config["retries"] ?: 0) + (other["retries"] ?: 0)
}
