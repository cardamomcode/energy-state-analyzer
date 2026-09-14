// clean — not flagged by parameter count
int cleanFewParams(int a, int b) {
    return a + b;
}

// flagged — parameter count (medium)
int flaggedManyParams(int a, int b, int c, int d, int e, int f) {
    return a + b + c + d + e + f;
}

// flagged — parameter count (high)
int flaggedTooManyParams(int a, int b, int c, int d, int e, int f, int g, int h, int i) {
    return a + b + c + d + e + f + g + h + i;
}
