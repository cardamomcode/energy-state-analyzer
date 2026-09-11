class OpaqueBooleans
{
    static void FlaggedPositionalBoolean() => Configure(true);
    static void FlaggedPositionalBooleanAmongOthers() => Process(1, false);
    static bool SuppressedNonCallUsage() => true;
    static void Configure(bool value) { }
    static void Process(int value, bool enabled) { }
}
