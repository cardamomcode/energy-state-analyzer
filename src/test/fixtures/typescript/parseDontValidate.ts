class Positive {
    constructor(readonly amount: number) {}
}

// flagged — parse, don't validate
function flaggedPositive(amount: number, limit: number): number {
    if (amount <= 0) { throw new Error("positive"); }
    return amount;
}

// flagged — parse, don't validate
function flaggedUpperBound(amount: number, limit: number): number {
    if (amount > 100) { throw new Error("positive"); }
    return amount;
}

// clean — not flagged by parse, don't validate
function cleanConstructed(amount: number, limit: number): Positive {
    if (amount <= 0) { throw new Error("positive"); }
    return new Positive(amount);
}

// clean — not flagged by parse, don't validate
function cleanTransformed(amount: number, limit: number): number {
    if (amount <= 0) { throw new Error("positive"); }
    return amount + 1;
}

// clean — not flagged by parse, don't validate
function cleanIdentity(amount: number, limit: number): number {
    return amount;
}

// clean — not flagged by parse, don't validate
function cleanOtherInput(amount: number, limit: number): number {
    if (limit <= 0) { throw new Error("positive"); }
    return amount;
}

// clean — not flagged by parse, don't validate
function cleanNestedThrow(amount: number, limit: number): number {
    if (amount <= 0) { if (limit < 0) { throw new Error("positive"); } }
    return amount;
}

// clean — not flagged by parse, don't validate
function cleanInterveningWork(amount: number, limit: number): number {
    if (amount <= 0) { throw new Error("positive"); }
    console.log(amount);
    return amount;
}


// flagged — parse, don't validate
function flaggedNonEmpty(items: number[]): number[] {
    if (items.length === 0) { throw new Error("empty"); }
    return items;
}

// clean — not flagged by parse, don't validate
function cleanNull(value: string | null): string {
    if (value === null) { throw new Error("missing"); }
    return value;
}

interface Checked {
    isNull(): boolean;
}

// flagged — parse, don't validate
function flaggedOpaqueNullCheck(value: Checked): Checked {
    if (value.isNull()) { throw new Error("missing"); }
    return value;
}

// clean — not flagged by parse, don't validate
function hasMissingValue(_value: string | null, _marker: null): boolean {
    return true;
}

// flagged — parse, don't validate
function flaggedOpaqueNullArgument(value: string | null): string | null {
    if (hasMissingValue(value, null)) { throw new Error("missing"); }
    return value;
}

// flagged — parse, don't validate
function flaggedDispatch(command: string): string {
    if (command === "quit") { throw new Error("bye"); }
    return command;
}

// clean — not flagged by parse, don't validate
function cleanRefined(value: "ready" | "pending"): "ready" {
    if (value === "pending") { throw new Error("not ready"); }
    return value;
}

// clean — not flagged by parse, don't validate
function cleanUntyped(amount) {
    if (amount <= 0) { throw new Error("positive"); }
    return amount;
}

// clean — not flagged by parse, don't validate
function cleanCommentOnly(amount: number): number {
    // if (amount <= 0) { throw new Error("positive"); }
    return amount;
}


// flagged — parse, don't validate
function flaggedCheckOnly(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
}

// flagged — parse, don't validate
function flaggedExplicitEmpty(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
    return undefined;
}

// flagged — parse, don't validate
function flaggedBareReturn(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
    return;
}

// clean — not flagged by parse, don't validate
function cleanGuardThenWork(amount: number): void {
    if (amount <= 0) { throw new Error("bad"); }
    console.log(amount);
}

// clean — not flagged by parse, don't validate
function cleanNoCheck(amount: number): void {
    return undefined;
}

// clean — not flagged by parse, don't validate
function cleanConditionalThrow(amount: number): void {
    if (amount <= 0) { if (amount < -1) { throw new Error("bad"); } }
}

// clean — not flagged by parse, don't validate
function cleanUnconditionalThrow(amount: number): void {
    throw new Error("bad");
}

// clean — not flagged by parse, don't validate
function cleanAssertion(amount: number): asserts amount is 1 {
    if (amount !== 1) { throw new Error("not one"); }
}

// flagged — parse, don't validate
function flaggedUntypedCheck(amount): void {
    if (amount <= 0) { throw new Error("bad"); }
}

class CheckedAmount {
    constructor(readonly amount: number) {
        if (amount <= 0) { throw new Error("positive"); }
    }
}
