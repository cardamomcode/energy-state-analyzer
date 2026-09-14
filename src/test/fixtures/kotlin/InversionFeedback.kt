// clean — not flagged by inversion feedback
fun cleanRequiredFollowup(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Unit {
    if (a) {
        if (b) {
            process()
        }
    }
    finish()
}


// clean — not flagged by inversion feedback
fun cleanDominantFollowup(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Unit {
    if (a) {
        recordAttempt()
        processRequest()
        recordResult()
    }
    finish()
}


// clean — not flagged by inversion feedback
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


// clean — not flagged by inversion feedback
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


// clean — not flagged by inversion feedback
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


// clean — not flagged by inversion feedback
fun cleanTwoLevels(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    prepare()
    if (a) {
        if (b) {
            process()
        }
    }
    return 0
}


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
fun flaggedImplicitFallthrough(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Unit {
    if (a) {
        if (b) {
            process()
        }
    }
}


// clean — not flagged by inversion feedback
fun cleanCommentHeavyBlock(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        /* This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block.  */
        process()
        return 1
    }
    return 0
}


// clean — not flagged by inversion feedback
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


// flagged — inversion feedback (medium)
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


// clean — not flagged by inversion feedback
fun cleanUnbracedElse(a: Boolean, b: Boolean, c: Boolean, d: Boolean): Int {
    if (a) {
        if (b) { return 1 }
    } else return 2
    return 0
}
