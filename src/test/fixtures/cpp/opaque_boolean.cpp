// flagged — opaque boolean
void flaggedPositionalBoolean() {
    configure(true);
}

// flagged — opaque boolean
void flaggedPositionalBooleanAmongOthers() {
    process(1, false);
}

// flagged in production; intentional expected values in test files
void flaggedAssertionBoolean() {
    EXPECT_EQ(isEnabled(), true);
    EXPECT_EQ(isDisabled(), false);
}

// clean — not flagged by opaque boolean
void suppressedLabeledAggregateField() {
    configure(Settings{.retries = true});
}

// clean — not flagged by opaque boolean
bool suppressedNonCallUsage() {
    bool ok = true;
    return ok;
}
