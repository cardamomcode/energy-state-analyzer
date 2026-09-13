fun cleanRequiredFollowup(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Unit {
    if (a) {
        if (b) {
            process()
        }
    }
    finish()
}


fun cleanDominantFollowup(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Unit {
    if (a) {
        recordAttempt()
        processRequest()
        recordResult()
    }
    finish()
}


fun cleanInterveningWork(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    prepare()
    if (a) {
        recordAttempt()
        if (b) {
            process()
        }
        recordResult()
    }
    return 0
}


fun cleanAlternativeBranch(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        if (b) {
            return 1
        }
    } else {
        return 2
    }
    return 0
}


fun cleanFlatAlternatives(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        return 1
    } else if (b) {
        return 2
    } else if (c) {
        return 3
    } else if (d) {
        return 4
    } else {
        return 0
    }
}


fun cleanTwoLevels(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    prepare()
    if (a) {
        if (b) {
            process()
        }
    }
    return 0
}


fun flaggedThreeLevels(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    prepare()
    if (a) {
        if (b) {
            if (c) {
                process()
            }
        }
    }
    return 0
}


fun flaggedFourLevels(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    prepare()
    if (a) {
        if (b) {
            if (c) {
                if (d) {
                    process()
                }
            }
        }
    }
    return 0
}


fun flaggedFiveGuards(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        if (b) {
            if (c) {
                if (d) {
                    if (a) {
                        return 1
                    }
                }
            }
        }
    }
    return 0
}


fun flaggedNestedElse(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        return 1
    } else {
        if (b) {
            return 2
        } else {
            if (c) {
                return 3
            }
        }
    }
    return 0
}


fun flaggedImplicitFallthrough(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Unit {
    if (a) {
        if (b) {
            process()
        }
    }
}


fun cleanCommentHeavyBlock(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        /* This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block.  */
        process()
        return 1
    }
    return 0
}


fun cleanNestedFunction(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Unit {
    if (a) {
        if (b) {
            fun inner() {
                if (c) { process() }
            }
            inner()
        }
    }
    finish()
}


fun flaggedAlternativeBody(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        return 1
    } else if (b) {
        if (c) {
            if (d) { process() }
        }
    }
    return 0
}


fun cleanUnbracedElse(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        if (b) { return 1 }
    } else return 2
    return 0
}
