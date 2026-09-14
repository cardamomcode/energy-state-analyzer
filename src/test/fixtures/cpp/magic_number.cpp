constexpr int MAX_RETRIES = 5;

// clean — not flagged by magic number
int cleanCommonValues(int x) {
    int total = x * 1;
    return total + 0;
}

// flagged — magic number
double flaggedMagicNumbers(double price) {
    double total = price * 1.08;
    if (total > 50) {
        total += 15.75;
    }
    return total;
}

// clean — not flagged by magic number
int exemptIndexAndDefault(int* values, int weight = 42) {
    int first = values[0];
    return first + weight;
}

// clean — not flagged by magic number
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
