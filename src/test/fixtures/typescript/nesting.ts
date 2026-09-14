// clean — not flagged by nesting
function cleanShallowNesting(x: number): number {
    if (x > 0) {
        if (x > 10) {
            return x;
        }
    }
    return 0;
}

// flagged — nesting (medium)
function flaggedDeepNesting(x: number): number {
    if (x > 0) {
        if (x > 1) {
            if (x > 2) {
                if (x > 3) {
                    if (x > 4) {
                        return x;
                    }
                }
            }
        }
    }
    return 0;
}

// flagged — nesting (high)
function flaggedSevereNesting(x: number): number {
    if (x > 0) {
        if (x > 1) {
            if (x > 2) {
                if (x > 3) {
                    if (x > 4) {
                        if (x > 5) {
                            if (x > 6) {
                                return x;
                            }
                        }
                    }
                }
            }
        }
    }
    return 0;
}

// flagged — nesting
function flaggedTryNesting(x: number): number {
    try {
        try {
            try {
                try {
                    try {
                        return x;
                    } catch (e) {
                        return 0;
                    }
                } catch (e) {
                    return 0;
                }
            } catch (e) {
                return 0;
            }
        } catch (e) {
            return 0;
        }
    } catch (e) {
        return 0;
    }
}
