// decision: mirrors the real-world F#-style Seq module pattern this fixture
// regression-tests against (expression/collections/seq.py) - one verb per operation, no
// shared name prefix, but nearly every function touches List<T>. Must NOT be flagged
// as function-count sprawl despite exceeding the generic 12-function threshold with no
// naming cohesion at all.

fun <T, U> map(source: List<T>, mapper: (T) -> U): List<U> {
    return source.map(mapper)
}

fun <T> filter(source: List<T>, predicate: (T) -> Boolean): List<T> {
    return source.filter(predicate)
}

fun <T, U> fold(source: List<T>, seed: U, folder: (U, T) -> U): U {
    var state = seed
    for (x in source) {
        state = folder(state, x)
    }
    return state
}

fun <T> head(source: List<T>): T {
    return source.first()
}

fun <T> length(source: List<T>): Int {
    return source.size
}

fun <T> take(source: List<T>, count: Int): List<T> {
    return source.take(count)
}

fun <T> skip(source: List<T>, count: Int): List<T> {
    return source.drop(count)
}

fun <T> tail(source: List<T>): List<T> {
    return skip(source, 1)
}

fun <T> concat(a: List<T>, b: List<T>): List<T> {
    return a + b
}

fun <T> reverse(source: List<T>): List<T> {
    return source.reversed()
}

fun <T> distinct(source: List<T>): List<T> {
    return source.distinct()
}

fun sum(source: List<Int>): Int {
    return source.sum()
}

fun max(source: List<Int>): Int {
    return source.max()
}
