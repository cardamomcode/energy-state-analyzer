// clean — not flagged by parameter count
fun cleanFewParams(a: Int, b: Int): Int {
    return a + b
}

// flagged — parameter count (medium)
fun flaggedManyParams(a: Int, b: Int, c: Int, d: Int, e: Int, f: Int): Int {
    return a + b + c + d + e + f
}

// flagged — parameter count (high)
fun flaggedTooManyParams(a: Int, b: Int, c: Int, d: Int, e: Int, f: Int, g: Int, h: Int, i: Int): Int {
    return a + b + c + d + e + f + g + h + i
}
