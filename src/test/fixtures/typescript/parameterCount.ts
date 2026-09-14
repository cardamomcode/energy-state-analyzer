// clean — not flagged by parameter count
function cleanFewParams(a: number, b: number): number {
    return a + b;
}

// flagged — parameter count (medium)
function flaggedManyParams(a: number, b: number, c: number, d: number, e: number, f: number): number {
    return a + b + c + d + e + f;
}

// flagged — parameter count (high)
function flaggedTooManyParams(a: number, b: number, c: number, d: number, e: number, f: number, g: number, h: number, i: number): number {
    return a + b + c + d + e + f + g + h + i;
}
