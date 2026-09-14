class OpaqueBooleans
{
// flagged — opaque boolean
    static void FlaggedPositionalBoolean() => Configure(true);
// flagged — opaque boolean
    static void FlaggedPositionalBooleanAmongOthers() => Process(1, false);
// clean — not flagged by opaque boolean
    static bool SuppressedNonCallUsage() => true;
    static void Configure(bool value) { }
    static void Process(int value, bool enabled) { }
}
