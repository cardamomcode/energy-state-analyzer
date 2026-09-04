fun cleanSimpleFunction(x: Int): Int {
    if (x > 0) {
        return 1
    }
    return 0
}

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
