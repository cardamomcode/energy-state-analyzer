int cleanMixedConditions(int a, std::string b, const char* c) {
    if (a > 10) {
        return 1;
    } else if (b == "urgent") {
        return 2;
    } else if (c == nullptr) {
        return 3;
    }
    return 0;
}

int flaggedThreeWayChain(int status) {
    if (status == 0xFE) {
        return 1;
    } else if (status == 42ULL) {
        return 2;
    } else if (status == 1'000) {
        return 3;
    }
    return 0;
}

int cleanStringChain(std::string status) {
    if (status == "open") {
        return 1;
    } else if (status == "closed") {
        return 2;
    } else if (status == "pending") {
        return 3;
    }
    return 0;
}

int cleanFloatingChain(double value) {
    if (value == 1.5) {
        return 1;
    } else if (value == 2.5) {
        return 2;
    } else if (value == 3e2) {
        return 3;
    }
    return 0;
}
