function cleanEarlyReturn(a: boolean, b: boolean): number {
    if (!a) {
        return 0;
    }
    if (!b) {
        return 0;
    }
    return 1;
}

function flaggedDominantIf(x: number): number {
    if (x > 0) {
        const a = 1;
        const b = 2;
        const c = 3;
        const d = 4;
        const e = 5;
        return a + b + c + d + e;
    }
    return 0;
}

function flaggedValidationChain(a: boolean, b: boolean, c: boolean): number {
    if (a) {
        if (b) {
            if (c) {
                return 1;
            }
        }
    }
    return 0;
}
