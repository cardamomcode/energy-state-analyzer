// flagged — error shadowing (high)
fun broadScope() {
    try {
        work0()
        work1()
        work2()
        work3()
        work4()
        work5()
        work6()
        work7()
    } catch (error: Failure0) {
        work10()
    }
}

// flagged — recovery dominance (high)
fun twoLineProtected() {
    try {
        work0()
        work1()
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
    }
}

// clean — not flagged by recovery dominance
fun oneLineProtected() {
    try {
        work0()
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
    }
}

// flagged — recovery dominance (medium)
fun workBefore() {
    work90()
    try {
        work0()
        work1()
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
    }
}

// flagged — recovery dominance (medium)
fun workAfter() {
    try {
        work0()
        work1()
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
    }
    work90()
}

// flagged — recovery dominance (high)
fun multilineLoop() {
    try {
        for (item in items) {
            process(item)
        }
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
    }
}

// clean — not flagged by recovery dominance
fun commentedTrivial() {
    try {
        work0() // mixed

        // only comment
    } catch (error: Failure0) {
        work10()

        // comment only
        /*
           multiline comment
        */
        work11()
        work12()
        work13()
        work14()
    }
}

// clean — not flagged by oversized recovery block
fun atLimit() {
    try {
        work0()
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()
    }
}

// flagged — oversized recovery block (medium)
fun overLimit() {
    try {
        work0()
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()
        work30()
    }
}

// clean — not flagged by oversized recovery block
fun commentedLimit() {
    try {
        work0() // mixed

        // only comment
    } catch (error: Failure0) {
        work10()

        // comment only
        /*
           multiline comment
        */
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()
    }
}

// clean — not flagged by oversized recovery block
fun separateHandlers() {
    try {
        work0()
    } catch (error: Failure0) {
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
    } catch (error: Failure1) {
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
    }
}

// flagged — oversized recovery block (medium)
fun oversizedCleanup() {
    try {
        work0()
    } finally {
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()
        work30()
    }
}

