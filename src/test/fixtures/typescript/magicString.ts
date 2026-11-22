// clean — not flagged by magic string
function cleanValues(name: string, config: Record<string, number>): [string, number] {
    const message = `user ${name} not found`;
    console.log("something went wrong");
    return [message, config["timeout"]];
}

// flagged — magic string
function flaggedMagicString(status: string): number {
    if (status === "pending") {
        return 1;
    }
    if (status === "pending") {
        return 2;
    }
    return 0;
}

// flagged — magic string
function flaggedDictKey(config: Record<string, number>, other: Record<string, number>): number {
    return config["retries"] + other["retries"];
}

// flagged — magic string
function flaggedCasePattern(event: string, other: string): number {
    switch (event) {
        case "started":
            return 1;
    }
    switch (other) {
        case "started":
            return 2;
    }
    return 0;
}

// clean — not flagged by magic string
function cleanCaseResult(event: string): string {
    switch (event) {
        case "opened":
            return "result";
        default:
            return "result";
    }
}

// flagged — magic string
function flaggedInequality(status: string, other: string): boolean {
    return status !== "archived" && other != "archived";
}
