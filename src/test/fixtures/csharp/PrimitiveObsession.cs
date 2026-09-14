class CleanPrimitiveExamples
{
// clean — not flagged by primitive obsession
    static string CleanDistinctTypes(string name, int age) => name;
}

class SwapRiskExamples
{
// flagged — primitive obsession
    static int FlaggedSwapRisk(int x, int y) => x + y;
}

class StringlyTypedExamples
{
// flagged — primitive obsession (low)
    static int FlaggedStringlyTyped(string status)
    {
        if (status == "pending") return 1;
        if (status == "active") return 2;
        if (status == "closed") return 3;
        return 0;
    }
}
