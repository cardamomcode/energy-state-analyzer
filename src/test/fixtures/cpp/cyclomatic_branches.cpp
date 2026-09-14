// clean — not flagged by cyclomatic complexity
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

// clean — not flagged by cyclomatic complexity
int classifyWithoutFallback(std::string value) {
    switch (value.size()) {
        case 1:
            return 1;
        case 2:
            return 2;
    }
    return 0;
}
