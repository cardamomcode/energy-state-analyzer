// decision: two classes with no shared inheritance, no type cross-reference, and no shared
// naming affix - a real grab-bag that should be flagged even though each class is small and
// internally cohesive.

class Logger {
    fun log(message: String) {
        println(message)
    }
}

class HttpClient {
    fun get(path: String): String {
        return path
    }
}
