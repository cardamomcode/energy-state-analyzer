function cleanRequiredFollowup(a: boolean, b: boolean, c: boolean, d: boolean): void {
    if (a) {
        if (b) {
            process();
        }
    }
    finish();
}


function cleanDominantFollowup(a: boolean, b: boolean, c: boolean, d: boolean): void {
    if (a) {
        recordAttempt();
        processRequest();
        recordResult();
    }
    finish();
}


function cleanInterveningWork(a: boolean, b: boolean, c: boolean, d: boolean): number {
    prepare();
    if (a) {
        recordAttempt();
        if (b) {
            process();
        }
        recordResult();
    }
    return 0;
}


function cleanAlternativeBranch(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        if (b) {
            return 1;
        }
    } else {
        return 2;
    }
    return 0;
}


function cleanFlatAlternatives(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        return 1;
    } else if (b) {
        return 2;
    } else if (c) {
        return 3;
    } else if (d) {
        return 4;
    } else {
        return 0;
    }
}


function cleanTwoLevels(a: boolean, b: boolean, c: boolean, d: boolean): number {
    prepare();
    if (a) {
        if (b) {
            process();
        }
    }
    return 0;
}


function flaggedThreeLevels(a: boolean, b: boolean, c: boolean, d: boolean): number {
    prepare();
    if (a) {
        if (b) {
            if (c) {
                process();
            }
        }
    }
    return 0;
}


function flaggedFourLevels(a: boolean, b: boolean, c: boolean, d: boolean): number {
    prepare();
    if (a) {
        if (b) {
            if (c) {
                if (d) {
                    process();
                }
            }
        }
    }
    return 0;
}


function flaggedFiveGuards(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        if (b) {
            if (c) {
                if (d) {
                    if (a) {
                        return 1;
                    }
                }
            }
        }
    }
    return 0;
}


function flaggedNestedElse(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        return 1;
    } else {
        if (b) {
            return 2;
        } else {
            if (c) {
                return 3;
            }
        }
    }
    return 0;
}


function flaggedImplicitFallthrough(a: boolean, b: boolean, c: boolean, d: boolean): void {
    if (a) {
        if (b) {
            process();
        }
    }
}


function cleanCommentHeavyBlock(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        /* This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block.  */
        process();
        return 1;
    }
    return 0;
}


function cleanNestedFunction(a: boolean, b: boolean, c: boolean, d: boolean): void {
    if (a) {
        if (b) {
            function inner() {
                if (c) { process(); }
            }
            inner();
        }
    }
    finish();
}


function flaggedAlternativeBody(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        return 1;
    } else if (b) {
        if (c) {
            if (d) { process(); }
        }
    }
    return 0;
}


function cleanUnbracedElse(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        if (b) { return 1; }
    } else return 2;
    return 0;
}
