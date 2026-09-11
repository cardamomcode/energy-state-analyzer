class EarlyReturns
{
    static int CleanEarlyReturn(bool a, bool b)
    {
        if (!a) return 0;
        if (!b) return 0;
        return 1;
    }
}

class DominantBlock
{
    static int FlaggedDominantIf(int x)
    {
        if (x > 0)
        {
            int a = 1;
            int b = 2;
            int c = 3;
            int d = 4;
            int e = 5;
            int f = 6;
            int g = 7;
            int h = 8;
            return a + b + c + d + e + f + g + h;
        }
        return 0;
    }
}

class ValidationChain
{
    static int FlaggedValidationChain(bool a, bool b, bool c)
    {
        if (a) { if (b) { if (c) { if (a && b && c) { return 1; } } } }
        return 0;
    }
}
