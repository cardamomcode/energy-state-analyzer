val cleanAnonymousCallable = { first: Int, second: Int -> first + second }

val flaggedAnonymousCyclomatic = { value: Int ->
    if (value == 0) return@flaggedAnonymousCyclomatic 0
    if (value == 1) return@flaggedAnonymousCyclomatic 1
    if (value == 2) return@flaggedAnonymousCyclomatic 2
    if (value == 3) return@flaggedAnonymousCyclomatic 3
    if (value == 4) return@flaggedAnonymousCyclomatic 4
    if (value == 5) return@flaggedAnonymousCyclomatic 5
    if (value == 6) return@flaggedAnonymousCyclomatic 6
    if (value == 7) return@flaggedAnonymousCyclomatic 7
    if (value == 8) return@flaggedAnonymousCyclomatic 8
    if (value == 9) return@flaggedAnonymousCyclomatic 9
    if (value == 10) return@flaggedAnonymousCyclomatic 10
    value
}

val flaggedSevereAnonymousCyclomatic = { value: Int ->
    if (value == 0) return@flaggedSevereAnonymousCyclomatic 0
    if (value == 1) return@flaggedSevereAnonymousCyclomatic 1
    if (value == 2) return@flaggedSevereAnonymousCyclomatic 2
    if (value == 3) return@flaggedSevereAnonymousCyclomatic 3
    if (value == 4) return@flaggedSevereAnonymousCyclomatic 4
    if (value == 5) return@flaggedSevereAnonymousCyclomatic 5
    if (value == 6) return@flaggedSevereAnonymousCyclomatic 6
    if (value == 7) return@flaggedSevereAnonymousCyclomatic 7
    if (value == 8) return@flaggedSevereAnonymousCyclomatic 8
    if (value == 9) return@flaggedSevereAnonymousCyclomatic 9
    if (value == 10) return@flaggedSevereAnonymousCyclomatic 10
    if (value == 11) return@flaggedSevereAnonymousCyclomatic 11
    if (value == 12) return@flaggedSevereAnonymousCyclomatic 12
    if (value == 13) return@flaggedSevereAnonymousCyclomatic 13
    if (value == 14) return@flaggedSevereAnonymousCyclomatic 14
    if (value == 15) return@flaggedSevereAnonymousCyclomatic 15
    value
}

val flaggedAnonymousCognitive = { value: Int ->
    if (value > 0) {
        if (value > 1) {
            if (value > 2) {
                if (value > 3) {
                    if (value > 4) {
                        if (value > 5) value
                    }
                }
            }
        }
    }
    0
}

val flaggedSevereAnonymousCognitive = { value: Int ->
    if (value > 0) {
        if (value > 1) {
            if (value > 2) {
                if (value > 3) {
                    if (value > 4) {
                        if (value > 5) {
                            if (value > 6) value
                        }
                    }
                }
            }
        }
    }
    0
}

val flaggedAnonymousParameters = { a: Int, b: Int, c: Int, d: Int, e: Int, f: Int ->
    a + b + c + d + e + f
}

val flaggedSevereAnonymousParameters = { a: Int, b: Int, c: Int, d: Int, e: Int, f: Int, g: Int, h: Int, i: Int ->
    a + b + c + d + e + f + g + h + i
}

fun nestedCallableBoundary(value: Int): Int {
    val callback = { item: Int -> if (item > 0) 1 else 0 }
    return callback(value)
}
