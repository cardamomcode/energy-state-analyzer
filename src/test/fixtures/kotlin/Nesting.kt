// clean — not flagged by nesting
fun cleanShallowNesting(x: Int): Int {
    if (x > 0) {
        if (x > 10) {
            return x
        }
    }
    return 0
}

// flagged — nesting (medium)
fun flaggedDeepNesting(x: Int): Int {
    if (x > 0) {
        if (x > 1) {
            if (x > 2) {
                if (x > 3) {
                    if (x > 4) {
                        return x
                    }
                }
            }
        }
    }
    return 0
}

// flagged — nesting (high)
fun flaggedSevereNesting(x: Int): Int {
    if (x > 0) {
        if (x > 1) {
            if (x > 2) {
                if (x > 3) {
                    if (x > 4) {
                        if (x > 5) {
                            if (x > 6) {
                                return x
                            }
                        }
                    }
                }
            }
        }
    }
    return 0
}

// flagged — nesting
fun flaggedTryNesting(x: Int): Int {
    try {
        try {
            try {
                try {
                    try {
                        return x
                    } catch (e: Exception) {
                        return 0
                    }
                } catch (e: Exception) {
                    return 0
                }
            } catch (e: Exception) {
                return 0
            }
        } catch (e: Exception) {
            return 0
        }
    } catch (e: Exception) {
        return 0
    }
}
