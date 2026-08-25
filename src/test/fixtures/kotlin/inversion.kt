fun cleanEarlyReturn(a: Boolean, b: Boolean): Int {
    if (!a) {
        return 0
    }
    if (!b) {
        return 0
    }
    return 1
}

fun flaggedDominantIf(x: Int): Int {
    if (x > 0) {
        val a = 1
        val b = 2
        val c = 3
        val d = 4
        val e = 5
        return a + b + c + d + e
    }
    return 0
}

fun flaggedValidationChain(a: Boolean, b: Boolean, c: Boolean): Int {
    if (a) {
        if (b) {
            if (c) {
                return 1
            }
        }
    }
    return 0
}

// Regression fixture: the outer if has an else branch, so it must not be mistaken for the
// first step of a guard-clause validation chain even though it nests two more levels of
// else-less ifs below it — Kotlin's else has no else_clause wrapper node, so detecting this
// relies on inversion.ts's fallback hasElse check (a second block child, or a nested if child).
fun cleanIfElseNotValidationChain(a: Boolean, b: Boolean, c: Boolean): Int {
    if (a) {
        if (b) {
            if (c) {
                return 1
            }
        }
    } else {
        return 2
    }
    return 0
}

fun cleanForOfSibling(items: IntArray): Int {
    if (items.size > 0) {
        if (items[0] > 0) {
            return items[0]
        }
    }
    for (item in items) {
        println(item)
    }
    return 0
}
