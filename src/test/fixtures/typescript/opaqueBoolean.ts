function flaggedPositionalBoolean() {
    configure(true);
}

function flaggedPositionalBooleanAmongOthers() {
    process(1, false);
}

function suppressedObjectLiteralField() {
    configure({ retries: true });
}

function suppressedNonCallUsage() {
    const ok = true;
    return ok;
}
