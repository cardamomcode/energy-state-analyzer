function cleanDistinctTypes(name: string, age: number): string {
    return `${name}:${age}`;
}

function flaggedSwapRisk(x: number, y: number): number {
    return x + y;
}

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
