// decision: regression fixture for a real false positive - two classes whose methods
// construct/return each other must NOT be flagged as unrelated just because there are 2 of
// them in one file.

class CancellationTokenSource {
    fun cancel() {}

    fun token(): CancellationToken {
        return CancellationToken()
    }
}

class CancellationToken {
    fun linkedSource(): CancellationTokenSource {
        return CancellationTokenSource()
    }

    fun isCancellationRequested(): Boolean {
        return false
    }
}
