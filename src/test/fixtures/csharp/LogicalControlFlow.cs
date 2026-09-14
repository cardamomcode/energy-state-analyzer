class LogicalControlFlow
{
    // clean — not flagged by logical control flow
    static void CleanExplicitIf(bool isLoggedIn)
    {
        if (isLoggedIn) Navigate();
    }

    static bool Navigate() => true;
}
