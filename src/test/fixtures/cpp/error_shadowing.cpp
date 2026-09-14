// clean — not flagged by error shadowing
int compute() {
    return 1;
}

// clean — not flagged by error shadowing
int transform(int value) {
    return value + 1;
}

// clean — not flagged by error shadowing
int finalize(int value) {
    return value * 2;
}

// decision: the protected try body is happy-path work and the small catch arm is recovery, so the
// error-shadowing detector should stay quiet.
// clean — not flagged by error shadowing
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

// clean — not flagged by error shadowing
int handleValueError(const std::exception& _err) {
    return -1;
}

// control: no error handling at all, so nothing should be flagged.
// clean — not flagged by error shadowing
int cleanPath() {
    int a = compute();
    int b = transform(a);
    return finalize(b);
}
