class CleanPrimitiveExamples
{
    static string CleanDistinctTypes(string name, int age) => name;
}

class SwapRiskExamples
{
    static int FlaggedSwapRisk(int x, int y) => x + y;
}

class StringlyTypedExamples
{
    static int FlaggedStringlyTyped(string status)
    {
        if (status == "pending") return 1;
        if (status == "active") return 2;
        if (status == "closed") return 3;
        return 0;
    }
}
