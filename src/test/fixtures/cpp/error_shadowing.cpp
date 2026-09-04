int compute() {
    return 1;
}

int transform(int value) {
    return value + 1;
}

int finalize(int value) {
    return value * 2;
}

// decision: most of this function's named nodes live inside the try/catch region, so error handling
// shadows the (tiny) unguarded business logic — the error-shadowing detector should flag it High.
int shadowedByError() {
    int result = 0;
    try {
        int value = compute();
        int processed = transform(value);
        result = finalize(processed);
    } catch (const std::exception& err) {
        result = handleValueError(err);
    }
    return result;
}

int handleValueError(const std::exception& _err) {
    return -1;
}

// control: no error handling at all, so nothing should be flagged.
int cleanPath() {
    int a = compute();
    int b = transform(a);
    return finalize(b);
}
