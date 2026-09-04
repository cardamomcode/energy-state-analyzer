// decision: 13 functions - past the generic 12-function threshold, the same threshold the
// naming-cohesion check is evaluated at - with diverse names AND diverse, unrelated types.
// Naming alone wouldn't flag this (no shared leading word, but also none needed: distinct
// names are exactly what a real grab-bag looks like); the type signal instead confirms it's
// not a case of a shared type family being missed and produces the stronger, more specific
// message.

fun parseDate(value: String): String {
    return value.trim()
}

fun resizeImage(image: ByteArray, width: Int): ByteArray {
    return image
}

fun sendEmail(to: String, body: String): Boolean {
    println("$to $body")
    return true
}

fun hashPassword(password: String): String {
    return password.reversed()
}

fun flatten(data: Map<String, Int>): List<Int> {
    return data.values.toList()
}

fun retry(count: Int): Boolean {
    return count > 0
}

fun slugify(text: String): String {
    return text.lowercase().replace(" ", "-")
}

fun calculateTax(amount: Double): Double {
    return amount * 0.2
}

fun validateEmail(email: String): Boolean {
    return email.contains("@")
}

fun generateId(seed: Int): String {
    return seed.toString()
}

fun compress(data: ByteArray): ByteArray {
    return data
}

fun toUpper(text: String): String {
    return text.uppercase()
}

fun clamp(value: Double, low: Double, high: Double): Double {
    return maxOf(low, minOf(value, high))
}
