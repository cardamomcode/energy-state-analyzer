std::string cleanDistinctTypes(std::string name, int age) {
    return name;
}

int flaggedSwapRisk(int x, int y) {
    return x + y;
}

int cleanDeclaratorShapes(int value, int* pointer, int& reference) {
    return value + *pointer + reference;
}

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
