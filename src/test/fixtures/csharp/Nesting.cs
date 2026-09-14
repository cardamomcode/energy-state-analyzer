class ShallowNesting
{
    static int CleanShallowNesting(int x)
    {
        if (x > 0) { if (x > 10) return x; }
        return 0;
    }
}

class DeepNesting
{
    static int FlaggedDeepNesting(int x)
    {
        if (x > 0) if (x > 1) if (x > 2) if (x > 3) if (x > 4) return x;
        return 0;
    }
}

class SevereNesting
{
    static int FlaggedSevereNesting(int x)
    {
        if (x > 0) if (x > 1) if (x > 2) if (x > 3) if (x > 4) if (x > 5) if (x > 6) return x;
        return 0;
    }
}

class TryNesting
{
    static int FlaggedTryNesting(int x)
    {
        try { try { try { try { try { return x; } catch { return 0; } } catch { return 0; } } catch { return 0; } } catch { return 0; } } catch { return 0; }
    }
}
