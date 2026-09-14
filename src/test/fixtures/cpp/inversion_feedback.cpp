void cleanRequiredFollowup(bool a, bool b, bool c, bool d) {
    if (a) {
        if (b) {
            process();
        }
    }
    finish();
}


void cleanDominantFollowup(bool a, bool b, bool c, bool d) {
    if (a) {
        recordAttempt();
        processRequest();
        recordResult();
    }
    finish();
}


int cleanInterveningWork(bool a, bool b, bool c, bool d) {
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


int cleanAlternativeBranch(bool a, bool b, bool c, bool d) {
    if (a) {
        if (b) {
            return 1;
        }
    } else {
        return 2;
    }
    return 0;
}


int cleanFlatAlternatives(bool a, bool b, bool c, bool d) {
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


int cleanTwoLevels(bool a, bool b, bool c, bool d) {
    prepare();
    if (a) {
        if (b) {
            process();
        }
    }
    return 0;
}


int flaggedThreeLevels(bool a, bool b, bool c, bool d) {
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


int flaggedFourLevels(bool a, bool b, bool c, bool d) {
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


int flaggedFiveGuards(bool a, bool b, bool c, bool d) {
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


int flaggedNestedElse(bool a, bool b, bool c, bool d) {
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


void flaggedImplicitFallthrough(bool a, bool b, bool c, bool d) {
    if (a) {
        if (b) {
            process();
        }
    }
}


int cleanCommentHeavyBlock(bool a, bool b, bool c, bool d) {
    if (a) {
        /* This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block.  */
        process();
        return 1;
    }
    return 0;
}


void cleanNestedFunction(bool a, bool b, bool c, bool d) {
    if (a) {
        if (b) {
            auto inner = [&]() {
                if (c) { process(); }
            };
            inner();
        }
    }
    finish();
}


int flaggedAlternativeBody(bool a, bool b, bool c, bool d) {
    if (a) {
        return 1;
    } else if (b) {
        if (c) {
            if (d) { process(); }
        }
    }
    return 0;
}


int cleanUnbracedElse(bool a, bool b, bool c, bool d) {
    if (a) {
        if (b) { return 1; }
    } else return 2;
    return 0;
}
