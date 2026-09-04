function compute(): number {
    return 1;
}

function transform(value: number): number {
    return value + 1;
}

function finalize(value: number): number {
    return value * 2;
}

// decision: most of this function's named nodes live inside the try/catch region, so error handling
// shadows the (tiny) unguarded business logic — the error-shadowing detector should flag it High.
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

function handleValueError(_err: unknown): number {
    return -1;
}

// control: no error handling at all, so nothing should be flagged.
export function cleanPath(): number {
    const a = compute();
    const b = transform(a);
    const c = finalize(b);
    return c;
}
