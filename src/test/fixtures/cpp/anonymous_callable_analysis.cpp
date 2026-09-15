auto cleanAnonymousCallable = [](int first, int second) { return first + second; };

auto flaggedAnonymousCyclomatic = [](int value) {
    if (value == 0) return 0;
    if (value == 1) return 1;
    if (value == 2) return 2;
    if (value == 3) return 3;
    if (value == 4) return 4;
    if (value == 5) return 5;
    if (value == 6) return 6;
    if (value == 7) return 7;
    if (value == 8) return 8;
    if (value == 9) return 9;
    if (value == 10) return 10;
    return value;
};

auto flaggedSevereAnonymousCyclomatic = [](int value) {
    if (value == 0) return 0;
    if (value == 1) return 1;
    if (value == 2) return 2;
    if (value == 3) return 3;
    if (value == 4) return 4;
    if (value == 5) return 5;
    if (value == 6) return 6;
    if (value == 7) return 7;
    if (value == 8) return 8;
    if (value == 9) return 9;
    if (value == 10) return 10;
    if (value == 11) return 11;
    if (value == 12) return 12;
    if (value == 13) return 13;
    if (value == 14) return 14;
    if (value == 15) return 15;
    return value;
};

auto flaggedAnonymousCognitive = [](int value) {
    if (value > 0) {
        if (value > 1) {
            if (value > 2) {
                if (value > 3) {
                    if (value > 4) {
                        if (value > 5) return value;
                    }
                }
            }
        }
    }
    return 0;
};

auto flaggedSevereAnonymousCognitive = [](int value) {
    if (value > 0) {
        if (value > 1) {
            if (value > 2) {
                if (value > 3) {
                    if (value > 4) {
                        if (value > 5) {
                            if (value > 6) return value;
                        }
                    }
                }
            }
        }
    }
    return 0;
};

auto flaggedAnonymousParameters = [](int a, int b, int c, int d, int e, int f) {
    return a + b + c + d + e + f;
};

auto flaggedSevereAnonymousParameters = [](int a, int b, int c, int d, int e, int f, int g, int h, int i) {
    return a + b + c + d + e + f + g + h + i;
};

auto flaggedAnonymousParameterForms = [](int a, int b = 0, int c = 0, int d = 0, int e = 0, auto... rest) {
    return a + b + c + d + e + sizeof...(rest);
};

int nestedCallableBoundary(int value) {
    auto callback = [](int item) { return item > 0 ? 1 : 0; };
    return callback(value);
}
