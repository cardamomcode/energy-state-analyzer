class CleanStrings
{
    static string CleanValues(string name) => "user " + name;
}

class FlaggedStrings
{
    static int FlaggedMagicString(string status)
    {
        if (status == "pending") return 1;
        if (status == "pending") return 2;
        return 0;
    }
}
