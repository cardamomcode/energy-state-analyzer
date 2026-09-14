// clean — not flagged by inversion
function cleanEarlyReturn(a: boolean, b: boolean): number {
    if (!a) {
        return 0;
    }
    if (!b) {
        return 0;
    }
    return 1;
}

// flagged — inversion
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

// flagged — inversion
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

// Regression fixture: a for-of loop sibling to a 2-deep nested if should not be mistaken
// for a validation chain — the for-of loop is unrelated control flow, not another guard step.
// clean — not flagged by inversion
function cleanForOfSibling(items: number[]): number {
    if (items.length > 0) {
        if (items[0] > 0) {
            return items[0];
        }
    }
    for (const item of items) {
        console.log(item);
    }
    return 0;
}
