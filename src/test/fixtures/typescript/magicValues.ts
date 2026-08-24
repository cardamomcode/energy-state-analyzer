const MAX_RETRIES = 5;

function cleanCommonValues(x: number): number {
    const total = x * 100;
    return total + 1;
}

function flaggedMagicNumbers(weight: number): number {
    let cost = 5.5;
    if (weight > 50) {
        cost += 15.75;
    }
    return cost;
}

function flaggedMagicString(): string {
    return "invalid input value not found";
}
