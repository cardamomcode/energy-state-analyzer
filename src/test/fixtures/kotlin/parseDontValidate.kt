data class Positive(val amount: Int)

fun flaggedPositive(amount: Int, limit: Int): Int {
    if (amount <= 0) { throw Exception("positive") }
    return amount;
}

fun flaggedUpperBound(amount: Int, limit: Int): Int {
    if (amount > 100) { throw Exception("positive") }
    return amount;
}

fun cleanConstructed(amount: Int, limit: Int): Positive {
    if (amount <= 0) { throw Exception("positive") }
    return Positive(amount);
}

fun cleanTransformed(amount: Int, limit: Int): Int {
    if (amount <= 0) { throw Exception("positive") }
    return amount + 1;
}

fun cleanIdentity(amount: Int, limit: Int): Int {
    return amount;
}

fun cleanOtherInput(amount: Int, limit: Int): Int {
    if (limit <= 0) { throw Exception("positive") }
    return amount;
}

fun cleanNestedThrow(amount: Int, limit: Int): Int {
    if (amount <= 0) { if (limit < 0) { throw Exception("positive") } }
    return amount;
}

fun cleanInterveningWork(amount: Int, limit: Int): Int {
    if (amount <= 0) { throw Exception("positive") }
    println(amount)
    return amount;
}


fun flaggedNonEmpty(items: List<Int>): List<Int> {
    if (items.isEmpty()) { throw Exception("empty") }
    return items
}

fun cleanNull(value: String?): String {
    if (value == null) { throw Exception("missing") }
    return value
}

fun cleanNullable(value: String?, limit: Int): String? {
    if (value.isNullOrEmpty()) { throw Exception("missing") }
    return value
}

fun flaggedNullable(value: String?, limit: Int): String? {
    if (value?.isEmpty() == true) { throw Exception("empty") }
    return value
}

fun flaggedDispatch(command: String): String {
    if (command == "quit") { throw Exception("bye") }
    return command
}


fun flaggedCheckOnly(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
}

fun flaggedExplicitEmpty(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
    return Unit
}

fun flaggedBareReturn(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
    return;
}

fun cleanGuardThenWork(amount: Int): Unit {
    if (amount <= 0) { throw Exception("bad") }
    println(amount)
}

fun cleanNoCheck(amount: Int): Unit {
    return Unit
}

fun cleanConditionalThrow(amount: Int): Unit {
    if (amount <= 0) { if (amount < -1) { throw Exception("bad") } }
}

fun cleanUnconditionalThrow(amount: Int): Unit {
    throw Exception("bad")
}
