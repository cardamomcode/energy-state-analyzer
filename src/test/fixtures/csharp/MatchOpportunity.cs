class MixedConditions
{
    // clean — not flagged by match opportunity
    static int CleanMixedConditions(int a, string b, object c)
    {
        if (a > 10) return 1;
        else if (b == "urgent") return 2;
        else if (c == null) return 3;
        return 0;
    }

}

class ThreeWayChain
{
    // flagged — match opportunity (low)
    static int FlaggedThreeWayChain(int status)
    {
        if (status == 254) return 1;
        else if (status == 42) return 2;
        else if (status == 1000) return 3;
        return 0;
    }
}
