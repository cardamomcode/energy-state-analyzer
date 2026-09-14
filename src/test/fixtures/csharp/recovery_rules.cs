class RecoveryRules {
static void BroadScope() {
    try {
        work0();
        work1();
        work2();
        work3();
        work4();
        work5();
        work6();
        work7();
    } catch (Failure0 error) {
        work10();
    }
}

static void TwoLineProtected() {
    try {
        work0();
        work1();
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

static void OneLineProtected() {
    try {
        work0();
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

static void WorkBefore() {
    work90();
    try {
        work0();
        work1();
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

static void WorkAfter() {
    try {
        work0();
        work1();
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
    work90();
}

static void MultilineLoop() {
    try {
        while (ready()) {
            process();
        }
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

static void CommentedTrivial() {
    try {
        work0(); // mixed

        // only comment
    } catch (Failure0 error) {
        work10();

        // comment only
        /*
           multiline comment
        */
        work11();
        work12();
        work13();
        work14();
    }
}

static void AtLimit() {
    try {
        work0();
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
        work15();
        work16();
        work17();
        work18();
        work19();
        work20();
        work21();
        work22();
        work23();
        work24();
        work25();
        work26();
        work27();
        work28();
        work29();
    }
}

static void OverLimit() {
    try {
        work0();
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
        work15();
        work16();
        work17();
        work18();
        work19();
        work20();
        work21();
        work22();
        work23();
        work24();
        work25();
        work26();
        work27();
        work28();
        work29();
        work30();
    }
}

static void CommentedLimit() {
    try {
        work0(); // mixed

        // only comment
    } catch (Failure0 error) {
        work10();

        // comment only
        /*
           multiline comment
        */
        work11();
        work12();
        work13();
        work14();
        work15();
        work16();
        work17();
        work18();
        work19();
        work20();
        work21();
        work22();
        work23();
        work24();
        work25();
        work26();
        work27();
        work28();
        work29();
    }
}

static void SeparateHandlers() {
    try {
        work0();
    } catch (Failure0 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
        work15();
        work16();
        work17();
        work18();
        work19();
        work20();
    } catch (Failure1 error) {
        work10();
        work11();
        work12();
        work13();
        work14();
        work15();
        work16();
        work17();
        work18();
        work19();
        work20();
    }
}

static void OversizedCleanup() {
    try {
        work0();
    } finally {
        work10();
        work11();
        work12();
        work13();
        work14();
        work15();
        work16();
        work17();
        work18();
        work19();
        work20();
        work21();
        work22();
        work23();
        work24();
        work25();
        work26();
        work27();
        work28();
        work29();
        work30();
    }
}

}
