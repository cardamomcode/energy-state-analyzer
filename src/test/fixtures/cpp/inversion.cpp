int cleanEarlyReturn(bool a, bool b) {
    if (!a) {
        return 0;
    }
    if (!b) {
        return 0;
    }
    return 1;
}

int flaggedDominantIf(int x) {
    if (x > 0) {
        int a = 1;
        int b = 2;
        int c = 3;
        int d = 4;
        int e = 5;
        return a + b + c + d + e;
    }
    return 0;
}

int flaggedValidationChain(bool a, bool b, bool c) {
    if (a) {
        if (b) {
            if (c) {
                return 1;
            }
        }
    }
    return 0;
}

int cleanRangeLoopSibling(std::vector<int> items) {
    if (!items.empty()) {
        if (items[0] > 0) {
            return items[0];
        }
    }
    for (const int item : items) {
        consume(item);
    }
    return 0;
}
