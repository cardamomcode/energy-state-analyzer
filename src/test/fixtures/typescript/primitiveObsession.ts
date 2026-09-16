// clean — not flagged by primitive obsession
function cleanDistinctTypes(name: string, age: number): string {
    return `${name}:${age}`;
}

// flagged — primitive obsession
function flaggedSwapRisk(x: number, y: number): number {
    return x + y;
}

// flagged — primitive obsession (low)
function flaggedStringlyTyped(status: string): number {
    if (status === "pending") {
        return 1;
    } else if (status === "active") {
        return 2;
    } else if (status === "closed") {
        return 3;
    }
    return 0;
}

// flagged — boolean blindness
function flaggedBooleanBlindness(compress: boolean, notify: boolean): boolean {
    return compress && notify;
}
