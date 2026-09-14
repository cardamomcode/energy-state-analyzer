// clean — not flagged by cognitive complexity
fun cleanSimpleFunction(x: Int): Int {
    if (x > 0) {
        return 1
    }
    return 0
}

// flagged — cognitive complexity (medium)
fun flaggedComplexFunction(x: Int): Int {
    if (x > 0) {
        if (x > 1) {
            if (x > 2) {
                if (x > 3) {
                    if (x > 4) {
                        if (x > 5) {
                            return x
                        }
                    }
                }
            }
        }
    }
    return 0
}

// flagged — cognitive complexity (high)
fun flaggedSevereFunction(x: Int): Int {
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
