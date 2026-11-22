class CleanStrings
{
    // clean — not flagged by magic string
    static string CleanValues(string name) => "user " + name;

    // clean — not flagged by magic string
    static string CleanCaseResult(string evt) => evt switch
    {
        "opened" => "result",
        _ => "result",
    };
}

class FlaggedStrings
{
    // flagged — magic string
    static int FlaggedMagicString(string status)
    {
        if (status == "pending") return 1;
        if (status == "pending") return 2;
        return 0;
    }

    // flagged — magic string
    static int FlaggedCasePattern(string evt, object other)
    {
        switch (evt)
        {
            case "started": return 1;
        }
        return other is "started" or "stopped" ? 1 : 0;
    }
}
