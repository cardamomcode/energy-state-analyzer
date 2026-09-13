class Positive {
    constructor(readonly amount: number) {}
}

function flaggedPositive(amount: number, limit: number): number {
    if (amount <= 0) { throw new Error("positive"); }
    return amount;
}

function flaggedUpperBound(amount: number, limit: number): number {
    if (amount > 100) { throw new Error("positive"); }
    return amount;
}

function cleanConstructed(amount: number, limit: number): Positive {
    if (amount <= 0) { throw new Error("positive"); }
    return new Positive(amount);
}

function cleanTransformed(amount: number, limit: number): number {
    if (amount <= 0) { throw new Error("positive"); }
    return amount + 1;
}

function cleanIdentity(amount: number, limit: number): number {
    return amount;
}

function cleanOtherInput(amount: number, limit: number): number {
    if (limit <= 0) { throw new Error("positive"); }
    return amount;
}

function cleanNestedThrow(amount: number, limit: number): number {
    if (amount <= 0) { if (limit < 0) { throw new Error("positive"); } }
    return amount;
}

function cleanInterveningWork(amount: number, limit: number): number {
    if (amount <= 0) { throw new Error("positive"); }
    console.log(amount);
    return amount;
}


function flaggedNonEmpty(items: number[]): number[] {
    if (items.length === 0) { throw new Error("empty"); }
    return items;
}

function cleanNull(value: string | null): string {
    if (value === null) { throw new Error("missing"); }
    return value;
}

function cleanRefined(value: "ready" | "pending"): "ready" {
    if (value === "pending") { throw new Error("not ready"); }
    return value;
}

function cleanUntyped(amount) {
    if (amount <= 0) { throw new Error("positive"); }
    return amount;
}

function cleanCommentOnly(amount: number): number {
    // if (amount <= 0) { throw new Error("positive"); }
    return amount;
}


function flaggedCheckOnly(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
}

function flaggedExplicitEmpty(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
    return undefined;
}

function flaggedBareReturn(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
    return;
}

function cleanGuardThenWork(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
    console.log(amount);
}

function cleanNoCheck(amount: number): void {
    return undefined;
}

function cleanConditionalThrow(amount: number): void {
    if (amount <= 0) { if (amount < -1) { throw new Error("bad"); } }
}

function cleanUnconditionalThrow(amount: number): void {
    throw new Error("bad");
}

function cleanAssertion(amount: number): asserts amount is 1 {
    if (amount !== 1) { throw new Error("not one"); }
}

function flaggedUntypedCheck(amount): void {
    if (amount <= 0) { throw new Error("bad"); }
}

class CheckedAmount {
    constructor(readonly amount: number) {
        if (amount <= 0) { throw new Error("positive"); }
    }
}
