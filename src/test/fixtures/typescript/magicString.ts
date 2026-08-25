function cleanValues(name: string, config: Record<string, number>): [string, number] {
    const message = `user ${name} not found`;
    console.log("something went wrong");
    return [message, config["timeout"]];
}

function flaggedMagicString(status: string): number {
    if (status === "pending") {
        return 1;
    }
    if (status === "pending") {
        return 2;
    }
    return 0;
}

function flaggedDictKey(config: Record<string, number>, other: Record<string, number>): number {
    return config["retries"] + other["retries"];
}
