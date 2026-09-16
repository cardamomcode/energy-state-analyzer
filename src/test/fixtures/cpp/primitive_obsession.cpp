// clean — not flagged by primitive obsession
std::string cleanDistinctTypes(std::string name, int age) {
    return name;
}

// flagged — primitive obsession
int flaggedSwapRisk(int x, int y) {
    return x + y;
}

// clean — not flagged by primitive obsession
int cleanDeclaratorShapes(int value, int* pointer, int& reference) {
    return value + *pointer + reference;
}

// flagged — primitive obsession (low)
int flaggedStringlyTyped(std::string status) {
    if (status == "pending") {
        return 1;
    } else if (status == "active") {
        return 2;
    } else if (status == "closed") {
        return 3;
    }
    return 0;
}

// flagged — boolean blindness
bool flaggedBooleanBlindness(bool compress, bool notify) {
    return compress && notify;
}

// flagged — boolean blindness (non-adjacent)
bool flaggedNonAdjacentBooleanBlindness(bool compress, std::string name, bool notify) {
    return compress && notify && !name.empty();
}
