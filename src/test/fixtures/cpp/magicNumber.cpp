constexpr int MAX_RETRIES = 5;

int cleanCommonValues(int x) {
    int total = x * 1;
    return total + 0;
}

double flaggedMagicNumbers(double price) {
    double total = price * 1.08;
    if (total > 50) {
        total += 15.75;
    }
    return total;
}

int exemptIndexAndDefault(int* values, int weight = 42) {
    int first = values[0];
    return first + weight;
}

int cleanNegativeValue(bool flag) {
    if (flag) {
        return -1;
    }
    return 1;
}

struct Limits {
    static constexpr int NestedRetries = 17;
    enum Code { Accepted = 23 };
};
