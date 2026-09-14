function broadScope() {
    try {
        work0();
        work1();
        work2();
        work3();
        work4();
        work5();
        work6();
        work7();
    } catch (error) {
        work10();
    }
}

function twoLineProtected() {
    try {
        work0();
        work1();
    } catch (error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

function oneLineProtected() {
    try {
        work0();
    } catch (error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

function workBefore() {
    work90();
    try {
        work0();
        work1();
    } catch (error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

function workAfter() {
    try {
        work0();
        work1();
    } catch (error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
    work90();
}

function multilineLoop() {
    try {
        while (ready()) {
            process();
        }
    } catch (error) {
        work10();
        work11();
        work12();
        work13();
        work14();
    }
}

function commentedTrivial() {
    try {
        work0(); // mixed

        // only comment
    } catch (error) {
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

function atLimit() {
    try {
        work0();
    } catch (error) {
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

function overLimit() {
    try {
        work0();
    } catch (error) {
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

function commentedLimit() {
    try {
        work0(); // mixed

        // only comment
    } catch (error) {
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

function separateHandlers() {
    try {
        work0();
    } catch (error) {
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
    } finally {
        cleanup0();
        cleanup1();
        cleanup2();
        cleanup3();
        cleanup4();
        cleanup5();
        cleanup6();
        cleanup7();
        cleanup8();
        cleanup9();
        cleanup10();
    }
}

function oversizedCleanup() {
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

