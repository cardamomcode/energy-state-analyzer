import kotlin.contracts.contract

data class Positive(val amount: Int)

// flagged — parse, don't validate
fun flaggedPositive(amount: Int, limit: Int): Int {
    if (amount <= 0) { throw Exception("positive") }
    return amount;
}

// flagged — parse, don't validate
fun flaggedUpperBound(amount: Int, limit: Int): Int {
    if (amount > 100) { throw Exception("positive") }
    return amount;
}

// clean — not flagged by parse, don't validate
fun cleanConstructed(amount: Int, limit: Int): Positive {
    if (amount <= 0) { throw Exception("positive") }
    return Positive(amount);
}

// clean — not flagged by parse, don't validate
fun cleanTransformed(amount: Int, limit: Int): Int {
    if (amount <= 0) { throw Exception("positive") }
    return amount + 1;
}

// clean — not flagged by parse, don't validate
fun cleanIdentity(amount: Int, limit: Int): Int {
    return amount;
}

// clean — not flagged by parse, don't validate
fun cleanOtherInput(amount: Int, limit: Int): Int {
    if (limit <= 0) { throw Exception("positive") }
    return amount;
}

// clean — not flagged by parse, don't validate
fun cleanNestedThrow(amount: Int, limit: Int): Int {
    if (amount <= 0) { if (limit < 0) { throw Exception("positive") } }
    return amount;
}

// clean — not flagged by parse, don't validate
fun cleanInterveningWork(amount: Int, limit: Int): Int {
    if (amount <= 0) { throw Exception("positive") }
    println(amount)
    return amount;
}


// flagged — parse, don't validate
fun flaggedNonEmpty(items: List<Int>): List<Int> {
    if (items.isEmpty()) { throw Exception("empty") }
    return items
}

// clean — not flagged by parse, don't validate
fun cleanNull(value: String?): String {
    if (value == null) { throw Exception("missing") }
    return value
}

// flagged — parse, don't validate (low)
fun cleanNullable(value: String?, limit: Int): String? {
    if (value.isNullOrEmpty()) { throw Exception("missing") }
    return value
}

// flagged — parse, don't validate (low)
fun flaggedNullable(value: String?, limit: Int): String? {
    if (value?.isEmpty() == true) { throw Exception("empty") }
    return value
}

// flagged — parse, don't validate
fun flaggedDispatch(command: String): String {
    if (command == "quit") { throw Exception("bye") }
    return command
}


// flagged — parse, don't validate
fun flaggedCheckOnly(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
}

// flagged — parse, don't validate
fun flaggedExplicitEmpty(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
    return Unit
}

// flagged — parse, don't validate
fun flaggedBareReturn(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
    return;
}

// clean — not flagged by parse, don't validate
fun cleanGuardThenWork(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
    println(amount)
}

// clean — not flagged by parse, don't validate
fun cleanNoCheck(amount: Int): Unit {
    return Unit
}

// clean — not flagged by parse, don't validate
fun cleanConditionalThrow(amount: Int): Unit {
    if (amount <= 0) { if (amount < -1) { throw Exception("bad") } }
}

// clean — not flagged by parse, don't validate
fun cleanUnconditionalThrow(amount: Int): Unit {
    throw Exception("bad")
}


// flagged — parse, don't validate
fun flaggedBooleanValidator(pw: String): Boolean {
    if ('@' !in pw) { return false }
    return true
}

// flagged — parse, don't validate
fun flaggedThrowingBoolean(pw: String): Boolean {
    if (pw.isEmpty()) { throw Exception("empty") }
    return true
}

// flagged — parse, don't validate
fun flaggedNullBooleanValidator(value: String?): Boolean {
    if (value == null) { return false }
    return true
}

// clean — not flagged by parse, don't validate
fun cleanBooleanQuery(value: String?): Boolean {
    if (value == null) { return false }
    return value.length > 5
}

// clean — not flagged by parse, don't validate
fun cleanExplicitNarrowingValidator(value: String?): Boolean {
    contract { returns(false) implies (value != null) }
    if (value == null) { return false }
    return true
}

// clean — not flagged by parse, don't validate (a truthy literal from the guard branch is not a rejection)
fun cleanFlipped(value: String?): Boolean {
    if (value != null) { return true }
    return false
}
