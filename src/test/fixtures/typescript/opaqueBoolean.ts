// flagged — opaque boolean
function flaggedPositionalBoolean() {
    configure(true);
}

// flagged — opaque boolean
function flaggedPositionalBooleanAmongOthers() {
    process(1, false);
}

// clean — not flagged by opaque boolean
function suppressedObjectLiteralField() {
    configure({ retries: true });
}

// clean — not flagged by opaque boolean
function suppressedNonCallUsage() {
    const ok = true;
    return ok;
}
