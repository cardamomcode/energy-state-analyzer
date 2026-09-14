// clean — not flagged by inversion feedback
function cleanRequiredFollowup(a: boolean, b: boolean, c: boolean, d: boolean): void {
    if (a) {
        if (b) {
            process();
        }
    }
    finish();
}


// clean — not flagged by inversion feedback
function cleanDominantFollowup(a: boolean, b: boolean, c: boolean, d: boolean): void {
    if (a) {
        recordAttempt();
        processRequest();
        recordResult();
    }
    finish();
}


// clean — not flagged by inversion feedback
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


// clean — not flagged by inversion feedback
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


// clean — not flagged by inversion feedback
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


// clean — not flagged by inversion feedback
function cleanTwoLevels(a: boolean, b: boolean, c: boolean, d: boolean): number {
    prepare();
    if (a) {
        if (b) {
            process();
        }
    }
    return 0;
}


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
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


// flagged — inversion feedback (medium)
function flaggedImplicitFallthrough(a: boolean, b: boolean, c: boolean, d: boolean): void {
    if (a) {
        if (b) {
            process();
        }
    }
}


// clean — not flagged by inversion feedback
function cleanCommentHeavyBlock(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        /* This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block.  */
        process();
        return 1;
    }
    return 0;
}


// clean — not flagged by inversion feedback
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


// flagged — inversion feedback (medium)
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


// clean — not flagged by inversion feedback
function cleanUnbracedElse(a: boolean, b: boolean, c: boolean, d: boolean): number {
    if (a) {
        if (b) { return 1; }
    } else return 2;
    return 0;
}
