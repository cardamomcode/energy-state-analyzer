function classify(value: string): number {
    switch (value) {
        case "a":
            return 1;
        case "b":
            return 2;
        default:
            return 0;
    }
}

function classifyWithoutFallback(value: string): number {
    switch (value) {
        case "a":
            return 1;
        case "b":
            return 2;
    }
    return 0;
}
