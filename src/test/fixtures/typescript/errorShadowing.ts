// clean — not flagged by error shadowing
function compute(): number {
    return 1;
}

// clean — not flagged by error shadowing
function transform(value: number): number {
    return value + 1;
}

// clean — not flagged by error shadowing
function finalize(value: number): number {
    return value * 2;
}

// decision: the protected try body is happy-path work and the small catch arm is recovery, so the
// error-shadowing detector should stay quiet.
// clean — not flagged by error shadowing
export function shadowedByError(): number {
    let result = 0;
    try {
        const value = compute();
        const processed = transform(value);
        result = finalize(processed);
    } catch (err) {
        result = handleValueError(err);
    }
    return result;
}

// clean — not flagged by error shadowing
function handleValueError(_err: unknown): number {
    return -1;
}

// control: no error handling at all, so nothing should be flagged.
// clean — not flagged by error shadowing
export function cleanPath(): number {
    const a = compute();
    const b = transform(a);
    const c = finalize(b);
    return c;
}
