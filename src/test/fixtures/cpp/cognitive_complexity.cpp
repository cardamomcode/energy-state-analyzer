int cleanSimpleFunction(int x) {
    if (x > 0) {
        return 1;
    }
    return 0;
}

int flaggedComplexFunction(int x) {
    if (x > 0)
        if (x > 1)
            if (x > 2)
                if (x > 3)
                    if (x > 4)
                        if (x > 5)
                            return x;
    return 0;
}

int flaggedSevereFunction(int x) {
    if (x > 0) {
        if (x > 1) {
            if (x > 2) {
                if (x > 3) {
                    if (x > 4) {
                        if (x > 5) {
                            if (x > 6) {
                                return x;
                            }
                        }
                    }
                }
            }
        }
    }
    return 0;
}
