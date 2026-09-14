// clean — not flagged by error shadowing
fun compute(): Int = 1

// clean — not flagged by error shadowing
fun transform(value: Int): Int = value + 1

// clean — not flagged by error shadowing
fun finalize(value: Int): Int = value * 2

// decision: the protected try body is happy-path work and the small catch arm is recovery, so the
// error-shadowing detector should stay quiet.
// clean — not flagged by error shadowing
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

// clean — not flagged by error shadowing
private fun handleValueError(_err: Exception): Int = -1

// control: no error handling at all, so nothing should be flagged.
// clean — not flagged by error shadowing
fun cleanPath(): Int {
    val a = compute()
    val b = transform(a)
    return finalize(b)
}
