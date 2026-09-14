class SimpleCognitive
{
    static int CleanSimpleFunction(int x) => x > 0 ? 1 : 0;
}

class MediumCognitive
{
    static int FlaggedComplexFunction(int x)
    {
        if (x > 0) { if (x > 1) { if (x > 2) { if (x > 3) { if (x > 4) { if (x > 5) { return x; } } } } } }
        return 0;
    }
}

class HighCognitive
{
    static int FlaggedSevereFunction(int x)
    {
        if (x > 0) { if (x > 1) { if (x > 2) { if (x > 3) { if (x > 4) { if (x > 5) { if (x > 6) { return x; } } } } } } }
        return 0;
    }
}
