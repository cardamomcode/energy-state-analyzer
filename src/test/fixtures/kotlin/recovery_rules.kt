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

