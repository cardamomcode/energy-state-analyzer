class OpaqueBooleans
{
    // flagged — opaque boolean
    static void FlaggedPositionalBoolean() => Configure(true);
    // flagged — opaque boolean
    static void FlaggedPositionalBooleanAmongOthers() => Process(1, false);
    // flagged in production; intentional expected values in test files
    static void FlaggedAssertionBoolean()
    {
        Assert.Equal(true, IsEnabled());
        Assert.Equal(false, IsDisabled());
    }
    // clean — not flagged by opaque boolean
    static bool SuppressedNonCallUsage() => true;
    static void Configure(bool value) { }
    static void Process(int value, bool enabled) { }
}
