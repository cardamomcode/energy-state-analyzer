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

int flaggedThreeWayChain(std::string status) {
    if (status == "open") {
        return 1;
    } else if (status == "closed") {
        return 2;
    } else if (status == "pending") {
        return 3;
    }
    return 0;
}
