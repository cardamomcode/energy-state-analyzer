// flagged — opaque boolean
function flaggedPositionalBoolean() {
    configure(true);
}

// flagged — opaque boolean
function flaggedPositionalBooleanAmongOthers() {
    process(1, false);
}

// flagged in production; intentional expected values in test files
function flaggedAssertionBoolean() {
    assert.equal(isEnabled(), true);
    assert.equal(isDisabled(), false);
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
