void broadScope() {
    try {
        work0();
        work1();
        work2();
        work3();
        work4();
        work5();
        work6();
        work7();
    } catch (Failure0& error) {
        work10();
    }
}

void twoLineProtected() {
    try {
        work0();
        work1();
    } catch (Failure0& error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

void oneLineProtected() {
    try {
        work0();
    } catch (Failure0& error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

void workBefore() {
    work90();
    try {
        work0();
        work1();
    } catch (Failure0& error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

void workAfter() {
    try {
        work0();
        work1();
    } catch (Failure0& error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
    work90();
}

void multilineLoop() {
    try {
        while (ready()) {
            process();
        }
    } catch (Failure0& error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

void commentedTrivial() {
    try {
        work0(); // mixed

        // only comment
    } catch (Failure0& error) {
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

void atLimit() {
    try {
        work0();
    } catch (Failure0& error) {
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

void overLimit() {
    try {
        work0();
    } catch (Failure0& error) {
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

void commentedLimit() {
    try {
        work0(); // mixed

        // only comment
    } catch (Failure0& error) {
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

void separateHandlers() {
    try {
        work0();
    } catch (Failure0& error) {
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
    } catch (Failure1& error) {
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

void oversizedCleanup() {
    try {
        work0();
    } catch (Failure0& error) {
        work10();
    }
}

