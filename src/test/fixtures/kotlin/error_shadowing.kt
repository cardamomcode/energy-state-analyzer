fun compute(): Int = 1

fun transform(value: Int): Int = value + 1

fun finalize(value: Int): Int = value * 2

// decision: most of this function's named nodes live inside the try/catch region, so error handling
// shadows the (tiny) unguarded business logic — the error-shadowing detector should flag it High.
fun shadowedByError(): Int {
    var result = 0
    try {
        val value = compute()
        val processed = transform(value)
        result = finalize(processed)
    } catch (err: Exception) {
        result = handleValueError(err)
    }
    return result
}

private fun handleValueError(_err: Exception): Int = -1

// control: no error handling at all, so nothing should be flagged.
fun cleanPath(): Int {
    val a = compute()
    val b = transform(a)
    return finalize(b)
}
