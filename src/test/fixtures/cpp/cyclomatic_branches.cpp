int classify(std::string value) {
    switch (value.size()) {
        case 1:
            return 1;
        case 2:
            return 2;
        default:
            return 0;
    }
}

int classifyWithoutFallback(std::string value) {
    switch (value.size()) {
        case 1:
            return 1;
        case 2:
            return 2;
    }
    return 0;
}
