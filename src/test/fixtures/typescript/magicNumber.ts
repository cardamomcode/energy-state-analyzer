const MAX_RETRIES = 5;

// clean — not flagged by magic number
function cleanCommonValues(x: number): number {
    const total = x * 1;
    return total + 0;
}

// flagged — magic number
function flaggedMagicNumbers(price: number): number {
    let total = price * 1.08;
    if (total > 50) {
        total += 15.75;
    }
    return total;
}

// clean — not flagged by magic number
function exemptIndexAndDefault(arr: number[], weight: number = 42): number {
    const first = arr[0];
    return first + weight;
}

// clean — not flagged by magic number
function cleanNegativeValue(flag: boolean): number {
    if (flag) {
        return -1;
    }
    return 1;
}
