void flaggedPositionalBoolean() {
    configure(true);
}

void flaggedPositionalBooleanAmongOthers() {
    process(1, false);
}

void suppressedLabeledAggregateField() {
    configure(Settings{.retries = true});
}

bool suppressedNonCallUsage() {
    bool ok = true;
    return ok;
}
